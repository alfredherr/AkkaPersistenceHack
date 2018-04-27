using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Event;
using Akka.Logger.Serilog;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using test_akka_persistence.AtLeastOnceDelivery.Messages;

namespace test_akka_persistence.AtLeastOnceDelivery
{
    public class DBPersister : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger<SerilogLoggingAdapter>();
        private readonly MessageDispatcher _streamDispatcher;
         
        public DBPersister()
        {
             
            _streamDispatcher = Context.Dispatcher;
            Receive<PersistAccountState>(msg => _PersistToDB(msg));
        }
        
        private void _PersistToDB(PersistAccountState persistAccountState)
        {
            _log.Debug($"In {Self.Path.Name} on _PersistToDB() ..ConsistentHashKey: {persistAccountState.ConsistentHashKey} AccountNumber: {persistAccountState.Message.AccountNumber})");

            _Save(persistAccountState.Message);
            Sender.Tell(new Confirm(persistAccountState.DeliveryId), Self);
            
        }

        private void _Save(AccountState message)
        {
            
            var connection = new SqliteConnection($"Data Source=./db/AccountState_{Self.Path.Name.ToUpper()}.db");
            connection.Open();

           
            var createCommand = connection.CreateCommand();
            createCommand.CommandText =
                $"CREATE TABLE IF NOT EXISTS AccountState"+
                 "(  " +
                 "  id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                 "   PersistenceId int,"+
                 "   AccountState TEXT"+
                 ")";
            createCommand.ExecuteNonQuery();

            // There is no special API for inserting data in bulk. For the best performance,
            // follow this pattern.

            _log.Debug($"Inserting {message}.");
            var stopwatch = Stopwatch.StartNew();

            // Always use a transaction
            using (var transaction = connection.BeginTransaction())
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText =
                    @"
                    INSERT INTO AccountState 
                         ( PersistenceId ,AccountState)
                    VALUES ($PersistenceId, $AccountState)
                ";

                // Re-use the same parameterized SqliteCommand
                var persistenceIdParam = insertCommand.CreateParameter();
                persistenceIdParam.ParameterName = "$PersistenceId";
                insertCommand.Parameters.Add(persistenceIdParam);

                var accountStateParam = insertCommand.CreateParameter();
                accountStateParam.ParameterName = "$AccountState";
                insertCommand.Parameters.Add(accountStateParam);
                
                // No need to call Prepare() since it's done lazily during the first execution.
                insertCommand.Prepare();

                persistenceIdParam.Value = message.AccountNumber;
                accountStateParam.Value = JsonConvert.SerializeObject(message);
                
                insertCommand.ExecuteNonQuery();

                transaction.Commit();
            }

            _log.Debug($"Done. (took {stopwatch.ElapsedMilliseconds} ms)");
            connection.Close();
           
        }
    

        protected override void PreStart()
        {
            _log.Debug($"PreStart()");
            base.PreStart();
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        private Task<T> RunWithStreamDispatcher<T> (Func<T> fn) {
            var promise = new TaskCompletionSource<T> ();

            _streamDispatcher.Schedule (() => {
                try {
                    var result = fn ();
                    promise.SetResult (result);
                } catch (Exception e) {
                    promise.SetException (e);
                }
            });

            return promise.Task;
        }

    }
}