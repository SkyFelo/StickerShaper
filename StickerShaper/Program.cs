using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.InputFiles;

class Program
{
    static async Task Main()
    {
        string token = "your_bot_code";
        var botClient = new TelegramBotClient(token);

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Бот @{me.Username} запущен");

        botClient.OnMessage += async (sender, e) =>
        {
            var message = e.Message;
            if (message == null) return;

            if (message.Text != null && message.Text.StartsWith("/start"))
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Привет! Отправь изображение — верну 500x500.");
                return;
            }

            if (message.Photo != null && message.Photo.Length > 0)
            {
                var photo = message.Photo.OrderByDescending(p => p.FileSize).First();

                await ProcessFileAsync(botClient, message.Chat.Id, photo.FileId);
            }
            else if (message.Document != null && message.Document.MimeType != null && message.Document.MimeType.StartsWith("image"))
            {
                await ProcessFileAsync(botClient, message.Chat.Id, message.Document.FileId);
            }
            else
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Отправьте изображение (фото или файл).");
            }
        };

        botClient.StartReceiving();
        Console.WriteLine("Бот работает. Нажмите любую клавишу для остановки...");
        Console.ReadKey();
        botClient.StopReceiving();
    }

    static async Task ProcessFileAsync(TelegramBotClient botClient, long chatId, string fileId)
    {
        var file = await botClient.GetFileAsync(fileId);
        using var ms = new MemoryStream();
        await botClient.DownloadFileAsync(file.FilePath, ms);
        ms.Position = 0;

        using Image image = Image.Load(ms);

        int maxSize = 512;
        int newWidth, newHeight;

        float ratio = (float)image.Width / image.Height;
        bool isNearlySquare = ratio > 0.9f && ratio < 1.1f;

        if (isNearlySquare)
        {
            newWidth = newHeight = maxSize;
        }
        else
        {
            float scale = Math.Min((float)maxSize / image.Width, (float)maxSize / image.Height);
            newWidth = (int)Math.Round(image.Width * scale);
            newHeight = (int)Math.Round(image.Height * scale);
        }

        if (newWidth > 512 || newHeight > 512)
        {
            await botClient.SendTextMessageAsync(chatId,
                "К сожалению, размер изображения не подходит для стикеров. " +
                "Изображение должно вписываться в квадрат 512×512 (одна сторона — 512 пикселей, другая — 512 или меньше).");
            return;
        }

        image.Mutate(ctx => ctx.Resize(newWidth, newHeight));

        using var outStream = new MemoryStream();
        await image.SaveAsync(outStream, new PngEncoder());
        outStream.Position = 0;

        await botClient.SendDocumentAsync(
            chatId,
            new InputOnlineFile(outStream, $"resized_{newWidth}x{newHeight}.png"),
            $"Готово — {newWidth}×{newHeight} PNG"
        );
    }

}