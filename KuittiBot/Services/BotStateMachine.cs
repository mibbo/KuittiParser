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
        //private IUserDataCache _userDataCache;
        private IUserDataRepository _userDataRepository;
        private readonly List<StateTransition> _transitions = new List<StateTransition>();
        private static bool _isNewUser;

        public BotStateMachine(UpdateService updateService, /*IUserDataCache userDataCache, */IUserDataRepository userDataRepository)
        {
            _updateService = updateService;
            //_userDataCache = userDataCache;
            _userDataRepository = userDataRepository;
            InitializeTransitions();
        }

        private void InitializeTransitions()
        {
            _transitions.Add(new StateTransition { CurrentState = BotState.WaitingForInput, Event = BotEvent.ReceivedReceiptDocument, NextState = BotState.ReceivedReceipt, Action = HandleReceipt });
            _transitions.Add(new StateTransition { CurrentState = BotState.ReceivedReceipt, Event = BotEvent.ReceivedTextMessage, NextState = BotState.AllocatingItems, Action = HandlePayers });
            //_transitions.Add(new StateTransition { CurrentState = BotState.AskingParticipants, Event = BotEvent.ReceivedTextMessage, NextState = BotState.AllocatingItems, Action = StartItemAllocation });
            _transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedCallbackQuery, NextState = BotState.AllocatingItems, Action = HandleItemAllocation });
            _transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedTextMessage, NextState = BotState.Summary, Action = ShowSummary });

            _transitions.Add(new StateTransition { CurrentState = BotState.WaitingForInput, Event = BotEvent.ReceivedCommand, NextState = BotState.WaitingForInput, Action = UserCommand });
            _transitions.Add(new StateTransition { CurrentState = BotState.ReceivedReceipt, Event = BotEvent.ReceivedCommand, NextState = BotState.ReceivedReceipt, Action = UserCommand });
            _transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedCommand, NextState = BotState.AllocatingItems, Action = UserCommand });
            _transitions.Add(new StateTransition { CurrentState = BotState.Summary, Event = BotEvent.ReceivedCommand, NextState = BotState.Summary, Action = UserCommand });
        }


        public async Task OnUpdate(Update update)
        {
            var message = _updateService.CheckMessageValidity(update);
            //if (!(update.Message is { } message)) return;

            var userId = message.From.Id.ToString();
            //var cachedUser = await _userDataCache.GetUserByIdAsync(userId);
            var cachedUser = await _userDataRepository.GetUserByIdAsync(userId);

            _isNewUser = cachedUser == null;
            var user = cachedUser ?? new UserDataEntity 
                {
                    UserId = userId,
                    UserName = message.From.Username,
                    CurrentState = BotState.WaitingForInput 
                };

            if (cachedUser == null)
            {
                await _userDataRepository.InsertAsync(user);
            }

            // Determine event and find transition
            var botEvent = DetermineEvent(update);
            var transition = _transitions.FirstOrDefault(t => t.CurrentState == user.CurrentState && t.Event == botEvent);

            // If the transition is succesfull, initialize next stage for the session
            if (transition != null)
            {


                await transition.Action(update);

                if (botEvent != BotEvent.ReceivedCommand)
                {
                    user.CurrentState = transition.NextState;
                    //user.CurrentState = BotState.WaitingForInput;               // DISABLED -> TODO next transition
                }
                //await _userDataCache.UpdateUserAsync(user);
                await _userDataRepository.UpdateUserAsync(user);
            }
        }

        private BotEvent DetermineEvent(Update update)
        {
            if (update.Type == UpdateType.Message && (update.Message.Document != null || update.Message.Photo != null))
            {
                return BotEvent.ReceivedReceiptDocument;
            }
            if (update.Type == UpdateType.Message && update.Message.Text.StartsWith('/'))
            {
                return BotEvent.ReceivedCommand;
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
            await _updateService.InitializeParsingForUser(update);
        }

        private async Task HandlePayers(Update update)
        {
            // Implement logic to handle receipt
            await _updateService.HandlePayersAndAskFirstProduct(update);
        }

        private async Task HandleItemAllocation(Update update)
        {
            // Implement logic to handle receipt
            await _updateService.HandleProductButtons(update);
        }

        public async Task ShowSummary(Update update)
        {
            await _updateService.PrintLeaderboard(update);
        }

        private async Task UserCommand(Update update)
        {

            // Implement logic to handle receipt
            if (_isNewUser)
            {
                await _updateService.WelcomeUser(update);
            }

            if (update.Message.Text.ToLower().Contains("/top"))
            {
                await _updateService.PrintLeaderboard(update);
            }

            if (update.Message.Text.ToLower().Contains("/delete"))
            {
                await _updateService.DeleteAllData(update);
            }

            if (update.Message.Text == "/CorrectTrainingLabels")
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
