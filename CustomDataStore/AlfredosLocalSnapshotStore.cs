using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Event;
using Akka.Logger.Serilog;
using Akka.Persistence;
using Akka.Persistence.Serialization;
using Akka.Persistence.Snapshot;
using Akka.Serialization;

namespace test_akka_persistence.CustomDataStore
{
    public class AlfredosLocalSnapshotStore : SnapshotStore
    {
        private static readonly Regex FilenameRegex = new Regex(@"^snapshot-(.+)-(\d+)-(\d+)", RegexOptions.Compiled);

        private readonly string _defaultSerializer;
        private readonly DirectoryInfo _dir;

        private readonly ILoggingAdapter _log;

        private readonly int _maxLoadAttempts;
        private readonly ISet<SnapshotMetadata> _saving;

        private readonly Serialization _serialization;
        private readonly MessageDispatcher _streamDispatcher;

        /// <summary>
        ///     TBD
        /// </summary>
        public AlfredosLocalSnapshotStore()
        {
            var config = Context.System.Settings.Config.GetConfig("akka.persistence.snapshot-store.alfredo-snapshoter");
            _maxLoadAttempts = config.GetInt("max-load-attempts");

            _streamDispatcher = Context.System.Dispatchers.Lookup(config.GetString("stream-dispatcher"));
            _dir = new DirectoryInfo(config.GetString("dir"));

            _defaultSerializer = config.GetString("serializer");

            _serialization = Context.System.Serialization;
            _saving = new SortedSet<SnapshotMetadata>(SnapshotMetadata.Comparer); // saving in progress
            _log = Context.GetLogger<SerilogLoggingAdapter>();

            _log.Info($"instantiated - in constructor.");
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="persistenceId">TBD</param>
        /// <param name="criteria">TBD</param>
        /// <returns>TBD</returns>
        protected override Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            _log.Info($"LoadAsync({persistenceId},{criteria}) called.");
            //
            // Heuristics:
            //
            // Select youngest `maxLoadAttempts` snapshots that match upper bound.
            // This may help in situations where saving of a snapshot could not be completed because of a VM crash.
            // Hence, an attempt to load that snapshot will fail but loading an older snapshot may succeed.
            //
            var metadata = GetSnapshotMetadata(persistenceId, criteria).Reverse().Take(_maxLoadAttempts).Reverse()
                .ToImmutableArray();
            return RunWithStreamDispatcher(() => Load(metadata));
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="metadata">TBD</param>
        /// <param name="snapshot">TBD</param>
        /// <returns>TBD</returns>
        protected override Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            _log.Info($"SaveAsync({metadata},{snapshot}) called.");
            _saving.Add(metadata);
            return RunWithStreamDispatcher(() =>
            {
                Save(metadata, snapshot);
                return new object();
            });
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="metadata">TBD</param>
        /// <returns>TBD</returns>
        protected override Task DeleteAsync(SnapshotMetadata metadata)
        {
            _log.Info($"DeleteAsync({metadata}) called.");
            _saving.Remove(metadata);
            return RunWithStreamDispatcher(() =>
            {
                // multiple snapshot files here mean that there were multiple snapshots for this seqNr, we delete all of them
                // usually snapshot-stores would keep one snapshot per sequenceNr however here in the file-based one we timestamp
                // snapshots and allow multiple to be kept around (for the same seqNr) if desired
                foreach (var file in GetSnapshotFiles(metadata)) file.Delete();
                return new object();
            });
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="persistenceId">TBD</param>
        /// <param name="criteria">TBD</param>
        /// <returns>TBD</returns>
        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            _log.Info($"DeleteAsync({persistenceId},{criteria}) called.");

            foreach (var metadata in GetSnapshotMetadata(persistenceId, criteria)) await DeleteAsync(metadata);
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="message">TBD</param>
        /// <returns>TBD</returns>
        protected override bool ReceivePluginInternal(object message)
        {
            _log.Info($"ReceivePluginInternal(object message) called.");

            if (message is SaveSnapshotSuccess)
            {
                _saving.Remove(((SaveSnapshotSuccess) message).Metadata);
            }
            else if (message is SaveSnapshotFailure)
            {
                // ignore
            }
            else if (message is DeleteSnapshotsSuccess)
            {
                // ignore
            }
            else if (message is DeleteSnapshotsFailure)
            {
                // ignore
            }
            else
            {
                return false;
            }

            return true;
        }

        private IEnumerable<FileInfo> GetSnapshotFiles(SnapshotMetadata metadata)
        {
            return GetSnapshotDir()
                .GetFiles("*", SearchOption.TopDirectoryOnly)
                .Where(f => SnapshotSequenceNrFilenameFilter(f, metadata));
        }

        private SelectedSnapshot Load(ImmutableArray<SnapshotMetadata> metadata)
        {
            var last = metadata.LastOrDefault();
            if (last == null)
                return null;
            try
            {
                return WithInputStream(last, stream =>
                {
                    var snapshot = Deserialize(stream);

                    return new SelectedSnapshot(last, snapshot.Data);
                });
            }
            catch (Exception ex)
            {
                var remaining = metadata.RemoveAt(metadata.Length - 1);
                _log.Error(ex, $"Error loading snapshot [{last}], remaining attempts: [{remaining.Length}]");
                if (remaining.IsEmpty)
                    throw;
                return Load(remaining);
            }
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="metadata">TBD</param>
        /// <param name="snapshot">TBD</param>
        protected virtual void Save(SnapshotMetadata metadata, object snapshot)
        {
            var tempFile = WithOutputStream(metadata, stream => { Serialize(stream, new Snapshot(snapshot)); });
            var newName = GetSnapshotFileForWrite(metadata);
            if (File.Exists(newName.FullName)) File.Delete(newName.FullName);
            tempFile.MoveTo(newName.FullName);
        }

        private Snapshot Deserialize(Stream stream)
        {
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            var snapshotType = typeof(Snapshot);
            var serializer = _serialization.FindSerializerForType(snapshotType, _defaultSerializer);
            var snapshot = (Snapshot) serializer.FromBinary(buffer, snapshotType);
            return snapshot;
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="stream">TBD</param>
        /// <param name="snapshot">TBD</param>
        protected void Serialize(Stream stream, Snapshot snapshot)
        {
            var serializer = _serialization.FindSerializerFor(snapshot, _defaultSerializer);
            var bytes = serializer.ToBinary(snapshot);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        ///     TBD
        /// </summary>
        /// <param name="metadata">TBD</param>
        /// <param name="p">TBD</param>
        /// <returns>TBD</returns>
        protected FileInfo WithOutputStream(SnapshotMetadata metadata, Action<Stream> p)
        {
            var tmpFile = GetSnapshotFileForWrite(metadata, ".tmp");
            WithStream(new BufferedStream(new FileStream(tmpFile.FullName, FileMode.Create)), stream =>
            {
                p(stream);
                stream.Flush();
                return new object();
            });
            return tmpFile;
        }

        private T WithInputStream<T>(SnapshotMetadata metadata, Func<Stream, T> p)
        {
            return
                WithStream(
                    new BufferedStream(new FileStream(GetSnapshotFileForWrite(metadata).FullName, FileMode.Open)), p);
        }

        private T WithStream<T>(Stream stream, Func<Stream, T> p)
        {
            try
            {
                var result = p(stream);
                return result;
            }
            finally
            {
                stream.Dispose();
            }
        }

        // only by PersistenceId and SequenceNr, timestamp is informational - accommodates for older files
        private FileInfo GetSnapshotFileForWrite(SnapshotMetadata metadata, string extension = "")
        {
            var filename =
                $"snapshot-{Uri.EscapeDataString(metadata.PersistenceId)}-{metadata.SequenceNr}-{metadata.Timestamp.Ticks}{extension}";
            return new FileInfo(Path.Combine(GetSnapshotDir().FullName, filename));
        }

        private IEnumerable<SnapshotMetadata> GetSnapshotMetadata(string persistenceId,
            SnapshotSelectionCriteria criteria)
        {
            var snapshots = GetSnapshotDir()
                .EnumerateFiles("snapshot-" + Uri.EscapeDataString(persistenceId) + "-*", SearchOption.TopDirectoryOnly)
                .Select(ExtractSnapshotMetadata)
                .Where(metadata => metadata != null && IsMatch(criteria, metadata) && !_saving.Contains(metadata))
                .ToList();

            snapshots.Sort(SnapshotMetadata.Comparer);

            return snapshots;
        }

        private bool IsMatch(SnapshotSelectionCriteria criteria, SnapshotMetadata metadata)
        {
            return metadata.SequenceNr <= criteria.MaxSequenceNr && metadata.Timestamp <= criteria.MaxTimeStamp &&
                   metadata.SequenceNr >= criteria.MinSequenceNr && metadata.Timestamp >= criteria.MinTimestamp;
        }

        private SnapshotMetadata ExtractSnapshotMetadata(FileInfo fileInfo)
        {
            var match = FilenameRegex.Match(fileInfo.Name);
            if (match.Success)
            {
                var pid = Uri.UnescapeDataString(match.Groups[1].Value);
                var seqNrString = match.Groups[2].Value;
                var timestampTicks = match.Groups[3].Value;

                long sequenceNr, ticks;
                if (long.TryParse(seqNrString, out sequenceNr) && long.TryParse(timestampTicks, out ticks))
                    return new SnapshotMetadata(pid, sequenceNr, new DateTime(ticks));
            }

            return null;
        }

        /// <summary>
        ///     TBD
        /// </summary>
        protected override void PreStart()
        {
            _log.Info($"PreStart() called.");
            GetSnapshotDir();
            base.PreStart();
        }

        protected override void PostStop()
        {
            _log.Info($"PostStop() called.");
            base.PostStop();
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            _log.Info($"SupervisorStrategy() called.");
            return base.SupervisorStrategy();
        }

        private DirectoryInfo GetSnapshotDir()
        {
            if (!_dir.Exists || (_dir.Attributes & FileAttributes.Directory) == 0)
            {
                // try to create the directory, on failure double check if someone else beat us to it
                Exception exception;
                try
                {
                    _dir.Create();
                    exception = null;
                }
                catch (Exception e)
                {
                    exception = e;
                }
                finally
                {
                    _dir.Refresh();
                }

                if (exception != null || (_dir.Attributes & FileAttributes.Directory) == 0)
                    throw new IOException("Failed to create snapshot directory " + _dir.FullName, exception);
            }

            return _dir;
        }

        private static bool SnapshotSequenceNrFilenameFilter(FileInfo fileInfo, SnapshotMetadata metadata)
        {
            var match = FilenameRegex.Match(fileInfo.Name);
            if (match.Success)
            {
                var pid = Uri.UnescapeDataString(match.Groups[1].Value);
                var seqNrString = match.Groups[2].Value;
                var timestampTicks = match.Groups[3].Value;

                try
                {
                    return pid.Equals(metadata.PersistenceId) &&
                           long.Parse(seqNrString) == metadata.SequenceNr &&
                           (metadata.Timestamp == SnapshotMetadata.TimestampNotSpecified ||
                            long.Parse(timestampTicks) == metadata.Timestamp.Ticks);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private Task<T> RunWithStreamDispatcher<T>(Func<T> fn)
        {
            var promise = new TaskCompletionSource<T>();

            _streamDispatcher.Schedule(() =>
            {
                try
                {
                    var result = fn();
                    promise.SetResult(result);
                }
                catch (Exception e)
                {
                    promise.SetException(e);
                }
            });

            return promise.Task;
        }
    }
}