using Standard.AI.OpenAI.Models.Services.Foundations.ChatCompletions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Telegram;

public class TelegramService
{
    private const int AdminChatId = 392115754;
    private const string PrivateChatId = "";

    private readonly TelegramBotClient _botClient = new("");

    private readonly CancellationTokenSource _cancellationToken;
    private CancellationTokenSource _imagineToken;
    private readonly MidJourney _midJourney;

    public TelegramService(CancellationTokenSource cancellationToken, MidJourney midJourney)
    {
        _cancellationToken = cancellationToken;
        _midJourney = midJourney;
        _imagineToken = new CancellationTokenSource();
    }

    public async Task StartReceiving()
    {
        var cancellationToken = _cancellationToken.Token;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { }, // receive all update types
            ThrowPendingUpdates = true
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );
        await _botClient.SendTextMessageAsync(AdminChatId, "Bot is started", cancellationToken: cancellationToken);
    }

    private async Task HandleErrorAsync(ITelegramBotClient arg1, Exception arg2, CancellationToken arg3)
    {
        await _botClient.SendTextMessageAsync(AdminChatId, arg2.Message, cancellationToken: arg3);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken token)
    {
        switch (update.Type)
        {
            case UpdateType.CallbackQuery:
            {
                var data = update.CallbackQuery!.Data;
                if (data == null)
                    return;

                if (!data.StartsWith(EnumHelpers.GetEnumDescription(BotCommands.Choose)))
                {
                    var position = data.Replace(EnumHelpers.GetEnumDescription(BotCommands.Choose), "").Trim(' ');
                    var upscaleResponse = await _midJourney.Upscale(update.CallbackQuery!.Message!.Caption!, position);
                    if (upscaleResponse.imageURL != null)
                    {
                        await SendPhoto(upscaleResponse.imageURL, token);
                    }
                }

                break;
            }
            case UpdateType.Message:
            {
                if (update.Message!.Type == MessageType.Photo)
                {
                    var caption = update.Message!.Caption;
                    if (caption != null && caption.StartsWith(EnumHelpers.GetEnumDescription(BotCommands.PostToPublic)))
                    {
                        if (update.Message.Photo != null)
                        {
                            var msg = caption.Replace(EnumHelpers.GetEnumDescription(BotCommands.PostToPublic), "");
                            await _botClient.SendPhotoAsync(PrivateChatId,
                                InputFile.FromFileId(update.Message.Photo.Last().FileId), cancellationToken: token,
                                caption: msg);
                        }
                    }
                }

                if (update.Message!.Text.IsNotNullOrEmpty())
                {
                    if (update.Message.Text!.StartsWith(EnumHelpers.GetEnumDescription(BotCommands.Formula)))
                    {
                        await SendMessageToGptAndReturnAnswer(ConstantsMessages.FormulaMessage, token);
                        return;
                    }

                    if (update.Message.Text!.StartsWith(EnumHelpers.GetEnumDescription(BotCommands.StopImagine)))
                    {
                        await _botClient.SendTextMessageAsync(AdminChatId, "Cancel was send", cancellationToken: token);
                        _imagineToken.Cancel();
                        return;
                    }

                    if (update.Message.Text!.StartsWith(EnumHelpers.GetEnumDescription(BotCommands.SendToGpt)))
                    {
                        var msg = update.Message.Text.Replace(EnumHelpers.GetEnumDescription(BotCommands.SendToGpt),
                            "");
                        await SendMessageToGptAndReturnAnswer(msg, token);
                        return;
                    }

                    if (update.Message.Text.StartsWith(EnumHelpers.GetEnumDescription(BotCommands.Imagine)))
                    {
                        _imagineToken = new CancellationTokenSource();
                        var msg = update.Message.Text.Replace(EnumHelpers.GetEnumDescription(BotCommands.Imagine), "");
                        await Task.Factory.StartNew(() => Imagine(msg, _imagineToken.Token),
                            _imagineToken.Token);
                        return;
                    }

                    if (update.Message.Text.StartsWith(EnumHelpers.GetEnumDescription(BotCommands.PostToPublic)))
                    {
                        var msg = update.Message.Text.Replace(EnumHelpers.GetEnumDescription(BotCommands.PostToPublic),
                            "");
                        await _botClient.SendTextMessageAsync(PrivateChatId, msg, cancellationToken: token);
                        return;
                    }
                }

                break;
            }
            default:
                return;
        }
    }

    private async Task Imagine(string comingMessage, CancellationToken token)
    {
        var imagineResponse = await _midJourney.Imagine(comingMessage);
        var messageFromAdminChat = await _botClient.SendTextMessageAsync(AdminChatId,
            $"TaskID: {imagineResponse.taskId!}",
            cancellationToken: token);

        var imageUrl = await WaitMessageFromImagine(imagineResponse.taskId!, messageFromAdminChat, 300, token);
        await SendPhotoToAdmin(imageUrl, token, imagineResponse.taskId);
    }

    private async Task<string> WaitMessageFromImagine(string taskId, Message message, int timeoutInSeconds,
        CancellationToken token)
    {
        var end = DateTime.Now.AddSeconds(timeoutInSeconds);
        var messageId = message.MessageId;
        while (DateTime.Now <= end)
        {
            if (token.IsCancellationRequested)
            {
                await EditMessageFromAdmin(messageId, $"Imagine [{taskId}] was stopped", token);
                throw new OperationCanceledException();
            }

            var result = await _midJourney.Result(taskId);
            if (result.status.IsNotNullOrEmpty())
            {
                var newMessage = message.Text + $"\nStatus: {result.status}";
                if (result.percentage != null)
                {
                    newMessage += $"\nPercentage: {result.percentage}";
                }

                messageId = await EditMessageFromAdmin(messageId, newMessage, token);
            }
            else
            {
                var newMessage = "Fail to get status, perhaps your ask is incorrect";
                messageId = await EditMessageFromAdmin(messageId, newMessage, token);
            }

            if (result.imageURL.IsNotNullOrEmpty())
            {
                await DeleteMessageFromAdmin(messageId, token);
                return result.imageURL!;
            }

            await Task.Delay(1000, token);
        }

        throw new TimeoutException("Timeout while waiting Result from MidJourney");
    }

    private async Task SendPhotoToAdmin(string url, CancellationToken token, string? caption = null) //todo rename
    {
        IEnumerable<InlineKeyboardButton> keyboardMarkups = new[]
        {
            InlineKeyboardButton.WithCallbackData("1", "/choose 1"),
            InlineKeyboardButton.WithCallbackData("2", "/choose 2"),
            InlineKeyboardButton.WithCallbackData("3", "/choose 3"),
            InlineKeyboardButton.WithCallbackData("4", "/choose 4"),
        };
        await SendPhoto(url, token, caption, new InlineKeyboardMarkup(keyboardMarkups));
    }

    private async Task SendPhoto(string url, CancellationToken token, string? caption = null,
        IReplyMarkup? markup = null)
    {
        var sendTextResult =
            await _botClient.SendTextMessageAsync(AdminChatId, "Start download image", cancellationToken: token);
        var httpClient = new HttpClient();
        var bytes = await httpClient.GetByteArrayAsync(url, token);

        var stream = new MemoryStream(bytes);
        await _botClient.SendPhotoAsync(AdminChatId, InputFile.FromStream(stream), caption: caption,
            replyMarkup: markup, cancellationToken: token);
        await DeleteMessageFromAdmin(sendTextResult.MessageId, token);
    }

    private async Task<int> EditMessageFromAdmin(int messageId, string message, CancellationToken token)
    {
        try
        {
            return (await _botClient.EditMessageTextAsync(AdminChatId, messageId, message, cancellationToken: token))
                .MessageId;
        }
        catch
        {
            // ignored
            return messageId;
        }
    }

    private async Task DeleteMessageFromAdmin(int messageId, CancellationToken token)
    {
        await _botClient.DeleteMessageAsync(AdminChatId, messageId, token);
    }

    private async Task SendMessageToGptAndReturnAnswer(string message, CancellationToken token)
    {
        var sentMessage =
            await _botClient.SendTextMessageAsync(AdminChatId, "Message sent to GPT", cancellationToken: token);
        var result = await SendMessageToGpt(message);

        await EditMessageFromAdmin(sentMessage.MessageId, result[0].Content, token);
    }

    private async Task<ChatCompletionMessage[]> SendMessageToGpt(string message)
    {
        IOpenAIProxy chatOpenAi = new OpenAiProxy(
            apiKey: "",
            organizationId: "");

        return await chatOpenAi.SendChatMessage(message);
    }
}