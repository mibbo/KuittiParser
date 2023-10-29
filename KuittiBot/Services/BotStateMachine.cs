using Azure;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Azure.Documents;
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
        public BotState CurrentState { get; private set; } = BotState.WaitingForInput;

        public BotStateMachine(UpdateService updateService, IUserDataCache userDataCache)
        {
            _updateService = updateService;
            _userDataCache = userDataCache;
            InitializeTransitions();
        }

        private void InitializeTransitions()
        {
            _transitions.Add(new StateTransition { CurrentState = BotState.WaitingForInput, Event = BotEvent.ReceivedPdfDocument, NextState = BotState.ReceivingReceipt, Action = HandleReceipt });
            _transitions.Add(new StateTransition { CurrentState = BotState.ReceivingReceipt, Event = BotEvent.ReceivedTextMessage, NextState = BotState.AskingParticipants, Action = AskParticipants });
            _transitions.Add(new StateTransition { CurrentState = BotState.AskingParticipants, Event = BotEvent.ReceivedTextMessage, NextState = BotState.AllocatingItems, Action = StartItemAllocation });
            _transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedCallbackQuery, NextState = BotState.AllocatingItems, Action = HandleItemAllocation });
            _transitions.Add(new StateTransition { CurrentState = BotState.AllocatingItems, Event = BotEvent.ReceivedTextMessage, NextState = BotState.Summary, Action = ShowSummary });
        }

        public async Task<UserStateEntity> GetUserStateAsync(string userId)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<UserStateEntity>("UserState", userId);
                return response.Value;
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                return null; // Entity not found
            }
        }

        public async Task UpdateUserStateAsync(UserStateEntity userState)
        {
            await _tableClient.UpsertEntityAsync(userState);
        }

        public async Task OnUpdate(Update update)
        {
            // Retrieve user state from Table Storage
            var userId = update.Message.From.Id.ToString();
            var userState = await GetUserStateAsync(userId) ?? new UserStateEntity { PartitionKey = "UserState", RowKey = userId, CurrentState = BotState.WaitingForInput };

            // Determine event and find transition
            var botEvent = DetermineEvent(update);
            var transition = _transitions.FirstOrDefault(t => t.CurrentState == userState.CurrentState && t.Event == botEvent);

            if (transition != null)
            {
                userState.CurrentState = transition.NextState;
                await transition.Action(update);
                await UpdateUserStateAsync(userState); // Save updated state back to Table Storage
            }
        }

        private BotEvent DetermineEvent(Update update)
        {
            if (update.Type == UpdateType.Message && update.Message.Document != null)
            {
                return BotEvent.ReceivedPdfDocument;
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

        private Task HandleReceipt(Update update)
        {
            // Implement logic to handle receipt
            return Task.CompletedTask;
        }

        private Task AskParticipants(Update update)
        {
            // Implement logic to ask for participants' names
            return Task.CompletedTask;
        }

        private Task StartItemAllocation(Update update)
        {
            // Implement logic to start item allocation
            return Task.CompletedTask;
        }

        private Task HandleItemAllocation(Update update)
        {
            // Implement logic to handle item allocation
            return Task.CompletedTask;
        }

        private Task ShowSummary(Update update)
        {
            // Implement logic to show summary
            return Task.CompletedTask;
        }

        private async Task<BotState> GetCurrentStateAsync(string userId)
        {
            try
            {
                var response = await _userDataCache.GetEntityAsync<UserStateEntity>("UserState", userId);
                return response.Value.CurrentState;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return BotState.WaitingForInput; // Return default state if not found
            }
        }

        private async Task SetCurrentStateAsync(string userId, BotState state)
        {
            var entity = new UserStateEntity
            {
                PartitionKey = "UserState",
                RowKey = userId,
                CurrentState = state
            };

            await _userDataCache.UpsertEntityAsync(entity);
        }
    }
}
