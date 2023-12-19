using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Azure.Documents;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace KuittiBot.Functions.Services
{
    public class BotStateMachine : IBotStateMachine
    {
        private readonly UpdateService _updateService;
        private IUserDataCache _userDataCache;
        private readonly List<StateTransition> _transitions = new List<StateTransition>();
        private static bool _isNewUser;

        public BotStateMachine(UpdateService updateService, IUserDataCache userDataCache)
        {
            _updateService = updateService;
            _userDataCache = userDataCache;
            InitializeTransitions();
        }

        private void InitializeTransitions()
        {
            _transitions.Add(new StateTransition { CurrentState = BotState.WaitingForInput, Event = BotEvent.ReceivedReceiptDocument, NextState = BotState.ReceivingReceipt, Action = HandleReceipt });
            _transitions.Add(new StateTransition { CurrentState = BotState.WaitingForInput, Event = BotEvent.ReceivedTextMessage, NextState = BotState.ReceivingReceipt, Action = PrintCommand });
            //_transitions.Add(new StateTransition { CurrentState = BotState.AskingParticipants, Event = BotEvent.ReceivedTextMessage, NextState = BotState.AllocatingItems, Action = StartItemAllocation });
            //_transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedCallbackQuery, NextState = BotState.AllocatingItems, Action = HandleItemAllocation });
            //_transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedTextMessage, NextState = BotState.Summary, Action = ShowSummary });
        }



        public async Task OnUpdate(Update update)
        {
            // Retrieve user state from Table Storage
            if (!(update.Message is { } message))
                return;
          
            var userId = message.From.Id.ToString();

            var userFromCache = await _userDataCache.GetUserByIdAsync(userId);
            
            // If no user found from cache -> Initialize new user
            var user = userFromCache ?? 
                new UserDataCacheEntity 
                {
                    Id = userId,
                    UserName = update.Message.From.Username,
                    CurrentState = BotState.WaitingForInput 
                };



            // Determine event and find transition
            var botEvent = DetermineEvent(update);
            var transition = _transitions.FirstOrDefault(t => t.CurrentState == user.CurrentState && t.Event == botEvent);

            // If the transition is succesfull, initialize next stage for the session
            if (transition != null)
            {
                //user.CurrentState = transition.NextState;                 // DISABLED -> TODO next transition
                user.CurrentState = BotState.WaitingForInput;               // DISABLED -> TODO next transition

                await _userDataCache.UpdateUserAsync(user);

                await transition.Action(update);

                //await _userDataCache.UpdateUserAsync(user);
            }
        }

        private BotEvent DetermineEvent(Update update)
        {
            if (update.Type == UpdateType.Message && (update.Message.Document != null || update.Message.Photo != null))
            {
                return BotEvent.ReceivedReceiptDocument;
            }
            if (update.Type == UpdateType.Message)
            {
                return BotEvent.ReceivedTextMessage;
            }
            if (update.Type == UpdateType.CallbackQuery)
            {
                return BotEvent.ReceivedCallbackQuery;
            }

            return BotEvent.ReceivedTextMessage;
        }

        private async Task HandleReceipt(Update update)
        {
            // Implement logic to handle receipt
            await _updateService.InitializeParseingForUser(update);
        }

        private async Task PrintCommand(Update update)
        {

            // Implement logic to handle receipt
            if (_isNewUser)
            {
                await _updateService.WelcomeUser(update);
            }

            if (update.Message.Text.Contains("top")) ;
            {
                await _updateService.PrintLeaderboard(update);
            }

            if (update.Message.Text == "/CorrectTrainingLabels") ;
            {
                await _updateService.CorrectTrainingData(update);
            }
        }
        //private Task AskParticipants(Update update)
        //{
        //    // Implement logic to ask for participants' names
        //    return Task.CompletedTask;
        //}

        //private Task StartItemAllocation(Update update)
        //{
        //    // Implement logic to start item allocation
        //    return Task.CompletedTask;
        //}

        //private Task HandleItemAllocation(Update update)
        //{
        //    // Implement logic to handle item allocation
        //    return Task.CompletedTask;
        //}

        //private Task ShowSummary(Update update)
        //{
        //    // Implement logic to show summary
        //    return Task.CompletedTask;
        //}
        
    }
}
