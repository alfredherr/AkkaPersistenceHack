using System;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;
using Akka.Persistence;

namespace test_akka_persistence
{
    class SaverActor : ReceivePersistentActor
    {
        public override string PersistenceId => Self.Path.Name;

        private readonly ILoggingAdapter _log = Context.GetLogger<SerilogLoggingAdapter>();

        public SaverActor()
        {
            Recover<SnapshotOffer>(offer => _ProcessSnapshot(offer));

            Command<BootUp>(command => _BootUp(command));

            Command<DeleteSnapshotFailure>(cmd =>
                _log.Error($"In {Self.Path.Name} processing DeleteSnapshotFailure msg. {cmd}"));
            Command<DeleteSnapshotsFailure>(cmd =>
                _log.Error($"In {Self.Path.Name} processing DeleteSnapshotsFailure msg. {cmd}"));
            Command<SaveSnapshotFailure>(cmd =>
                _log.Error($"In {Self.Path.Name} processing SaveSnapshotFailure msg. {cmd}"));

            Command<DeleteSnapshotSuccess>(cmd =>
                _log.Info($"In {Self.Path.Name} processing DeleteSnapshotSuccess msg. {cmd}"));
            Command<DeleteSnapshotsSuccess>(cmd =>
                _log.Info($"In {Self.Path.Name} processing DeleteSnapshotsSuccess msg. {cmd}"));
            Command<SaveSnapshotSuccess>(cmd => _RemoveOldSnapshots(cmd));

            Command<SaveAnEvent>(cmd => _SaveEvent(cmd));
            Command<TakeSnapshot>(cmd => _TakeSnapshot());

            CommandAny(cmd => _log.Error($"In {Self.Path.Name} processing CommandAny msg. {cmd}"));
        }

        private void _ProcessSnapshot(SnapshotOffer offer)
        {
            _log.Info($"In {Self.Path.Name} on _ProcessSnapshot({offer})");
            
        }

        private void _BootUp(BootUp command)
        {
            _log.Info($"In {Self.Path.Name} on _BootUp({command})");
            _TakeSnapshot();


        }
        private void _SaveEvent(SaveAnEvent cmd)
        {
            PersistedEvent @event  = new PersistedEvent($"Event {LastSequenceNr} @ {DateTime.Now}");

            Persist(@event, e =>
            {
                _log.Info($"In {Self.Path.Name} _SaveEvent({@event}) persisted.");
                Self.Tell(new TakeSnapshot());
            });
        }

        private void _TakeSnapshot()
        {
            SaveSnapshot($"Snapshot This! @ {DateTime.Now}");
            _log.Info($"In {Self.Path.Name} on _TakeSnapshot() - first");
           
        }
        private void _RemoveOldSnapshots(SaveSnapshotSuccess cmd)
        {
            _log.Info($"In {Self.Path.Name} SaveSnapshotSuccess msg. {cmd}");
            _log.Info($"In {Self.Path.Name} _RemoveOldSnapshots() - cmd.Metadata.SequenceNr: {cmd.Metadata.SequenceNr}");
            Console.WriteLine($"cmd.Metadata.SequenceNr: {cmd.Metadata.SequenceNr} LastSequenceNr: {LastSequenceNr}");
            
            DeleteSnapshots(new SnapshotSelectionCriteria(cmd.Metadata.SequenceNr - 1));
            
        }

        public override void AroundPostRestart(Exception reason, object message)
        {
            //_log.Info($"In {Self.Path.Name} on AroundPostRestart(Exception reason, object message)");
            base.AroundPostRestart(reason, message);
        }

        public override void AroundPostStop()
        {
            //_log.Info($"In {Self.Path.Name} on AroundPostStop()");
            base.AroundPostStop();
        }

        public override void AroundPreRestart(Exception cause, object message)
        {
            //_log.Info($"In {Self.Path.Name} on AroundPreRestart(Exception cause, object message)");
            base.AroundPreRestart(cause, message);
        }

        public override void AroundPreStart()
        {
            //_log.Info($"In {Self.Path.Name} on AroundPreStart()");
            base.AroundPreStart();
        }

        protected override bool AroundReceive(Receive receive, object message)
        {
            //_log.Info($"In {Self.Path.Name} on AroundReceive(Receive receive, object message)");
            return base.AroundReceive(receive, message);
        }

        protected override void OnPersistFailure(Exception cause, object @event, long sequenceNr)
        {
            _log.Error($"In {Self.Path.Name} on OnPersistFailure(Exception cause, object @event, long sequenceNr)");
            base.OnPersistFailure(cause, @event, sequenceNr);
        }

        protected override void OnPersistRejected(Exception cause, object @event, long sequenceNr)
        {
            //_log.Info($"In {Self.Path.Name} on OnPersistRejected(Exception cause, object @event, long sequenceNr)");
            base.OnPersistRejected(cause, @event, sequenceNr);
        }

        protected override void OnRecoveryFailure(Exception reason, object message = null)
        {
            //_log.Info($"In {Self.Path.Name} on OnRecoveryFailure(Exception reason, object message = null)");
            base.OnRecoveryFailure(reason, message);
        }

        protected override void OnReplaySuccess()
        {
            //_log.Info($"In {Self.Path.Name} on OnReplaySuccess()");
            base.OnReplaySuccess();
        }

        protected override void PostRestart(Exception reason)
        {
            //_log.Info($"In {Self.Path.Name} on PostRestart(Exception reason)");
            base.PostRestart(reason);
        }

        protected override void PostStop()
        {
            //_log.Info($"In {Self.Path.Name} on PostStop()");
            base.PostStop();
        }

        protected override void PreRestart(Exception reason, object message)
        {
            //_log.Info($"In {Self.Path.Name} on PreRestart(Exception reason, object message)");
            base.PreRestart(reason, message);
        }

        protected override void PreStart()
        {
            //_log.Info($"In {Self.Path.Name} on PreStart()");
            Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(5), Self, new SaveAnEvent(), ActorRefs.NoSender);

            base.PreStart();
        }

        protected override bool Receive(object message)
        {
            //_log.Info($"In {Self.Path.Name} on Receive()");
            return base.Receive(message);
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            //_log.Info($"In {Self.Path.Name} on SupervisorStrategy()");
            return base.SupervisorStrategy();
        }

        protected override void Unhandled(object message)
        {
            //_log.Info($"In {Self.Path.Name} on Unhandled({message.GetType().Name} {message})");
            base.Unhandled(message);
        }

        public override Recovery Recovery => base.Recovery;

        public override IStashOverflowStrategy InternalStashOverflowStrategy => base.InternalStashOverflowStrategy;
    }

    internal class TakeSnapshot
    {
    }
    internal class SaveAnEvent
    {
    }
}