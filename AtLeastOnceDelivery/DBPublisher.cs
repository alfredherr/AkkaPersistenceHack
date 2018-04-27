using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;
using Akka.Persistence;
using Akka.Routing;
using test_akka_persistence.AtLeastOnceDelivery.Messages;
using test_akka_persistence.Messages;

namespace test_akka_persistence.AtLeastOnceDelivery
{
    public class DBPublisher : AtLeastOnceDeliveryReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger<SerilogLoggingAdapter>();
        
        public override string PersistenceId => Self.Path.Name;

        private static IActorRef _destionationActor;

        /**
         * 
         */
        public DBPublisher()
        {
            
//            Recover<MessageSent>(msgSent => _Deliver(msgSent));
//            
//            Recover<MsgConfirmed>(msgConfirmed => _ConfirmDelivery(msgConfirmed));

            
            Command<AccountState>(state => _SaveAccountState(state));
            
            Command<BootUp>(msg => _destionationActor = msg.ActorMessage);

            Command<Confirm>(confirm => _ConfirmDelivery(confirm));
            
            CommandAny(msg =>  _log.Error($"In {Self.Path.Name} processing CommandAny msg '{msg}'."));

        }

  

        private void _SaveAccountState(AccountState state)
        {
            //Saves it internally in case of system crash
            //Persist(new MessageSent(state), _Deliver );

            _Deliver(new MessageSent(state)); //Cheap insecure way -- for demo
        }


        private void _Deliver(MessageSent messageSent)
        {
            
            // Actuallly tries to send the message
            Deliver(_destionationActor.Path, l =>
            {
                _log.Debug($"In {Self.Path.Name} on _Deliver(MessageSent {messageSent.Message})");
                var accountState = messageSent.Message as AccountState;
                accountState.AccountNumber = l == 0 ? 1 : l + 1;
                return new PersistAccountState(l, accountState);
            });
        }

        private void _ConfirmDelivery(Confirm confirm)
        {
            _log.Debug($"In {Self.Path.Name} on _ConfirmDelivery(MsgConfirmed {confirm.DeliveryId})");
            ConfirmDelivery(confirm.DeliveryId);
        }
        
        protected override void PreStart()
        {
            _log.Debug($"In {Self.Path.Name} on PreStart()");
            Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0),
                TimeSpan.FromTicks(100), Self, new AccountState(), ActorRefs.NoSender);

            base.PreStart();
        }
        
    }
}