using Azure;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Azure.Documents;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public BotStateMachine(UpdateService updateService, IUserDataCache userDataCache)
        {
            _updateService = updateService;
            _userDataCache = userDataCache;
            InitializeTransitions();
        }

        private void InitializeTransitions()
        {
            _transitions.Add(new StateTransition { CurrentState = BotState.WaitingForInput, Event = BotEvent.ReceivedReceiptDocument, NextState = BotState.ReceivingReceipt, Action = HandleReceipt });
            //_transitions.Add(new StateTransition { CurrentState = BotState.ReceivingReceipt, Event = BotEvent.ReceivedTextMessage, NextState = BotState.AskingParticipants, Action = AskParticipants });
            //_transitions.Add(new StateTransition { CurrentState = BotState.AskingParticipants, Event = BotEvent.ReceivedTextMessage, NextState = BotState.AllocatingItems, Action = StartItemAllocation });
            //_transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedCallbackQuery, NextState = BotState.AllocatingItems, Action = HandleItemAllocation });
            //_transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedTextMessage, NextState = BotState.Summary, Action = ShowSummary });
        }

        public async Task<UserDataCacheEntity> GetUserStateAsync(string userId)
        {
            try
            {
                var userFromCache = await _userDataCache.GetUserById(userId);
                return userFromCache;
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                return null; // Entity not found
            }
        }

        public async Task UpdateUserStateAsync(UserDataCacheEntity userState)
        {
            await _userDataCache.UpdateUserStateAsync(userState);
        }

        public async Task OnUpdate(Update update)
        {
            // Retrieve user state from Table Storage
            if (!(update.Message is { } message))
                return;

            var userId = message.From.Id.ToString();
            var userState = await GetUserStateAsync(userId) ?? 
                new UserDataCacheEntity 
                {
                    Id = userId,
                    UserName = update.Message.From.Username,
                    CurrentState = BotState.WaitingForInput 
                };

            // Determine event and find transition
            var botEvent = DetermineEvent(update);
            var transition = _transitions.FirstOrDefault(t => t.CurrentState == userState.CurrentState && t.Event == botEvent);

            // If the transition is succesfull, initialize next stage for the session
            if (transition != null)
            {
                userState.CurrentState = transition.NextState;
                await transition.Action(update);

                userState.FileName = update.Message.Document?.FileName ?? update.Message.Photo?.LastOrDefault().FileUniqueId;
                userState.FileId = update.Message.Document?.FileId ?? update.Message.Photo?.LastOrDefault().FileId;
                //await UpdateUserStateAsync(userState); // Save updated state back to Table Storage
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
