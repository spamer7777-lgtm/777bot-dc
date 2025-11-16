using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.WebSocket;
using SkiaSharp;

public static class ProfileCardGenerator
{
    private static readonly HttpClient Http = new();

    public static async Task<byte[]> GenerateAsync(SocketUser user, UserData data)
    {
        const int width = 900;
        const int height = 300;

        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // --- TŁO ---
        canvas.Clear(new SKColor(10, 10, 20));

        // Jeśli user ma customowe tło, spróbuj wczytać
        if (!string.IsNullOrWhiteSpace(data.ProfileBackgroundUrl))
        {
            try
            {
                using var bgStream = await Http.GetStreamAsync(data.ProfileBackgroundUrl);
                using var bgData = SKData.Create(bgStream);
                using var bgBitmap = SKBitmap.Decode(bgData);
                if (bgBitmap != null)
                {
                    var dest = new SKRect(0, 0, width, height);
                    canvas.DrawBitmap(bgBitmap, dest);
                }
            }
            catch
            {
                // Jak się nie uda, lecimy dalej z gradientem
            }
        }

        // Gradient overlay
        using (var overlayPaint = new SKPaint())
        {
            overlayPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                new[]
                {
                    new SKColor(0, 0, 0, 200),
                    new SKColor(20, 0, 40, 180)
                },
                null,
                SKShaderTileMode.Clamp
            );
            canvas.DrawRect(new SKRect(0, 0, width, height), overlayPaint);
        }

        // Panel centralny
        using (var panelPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 160),
            IsAntialias = true
        })
        {
            var rect = new SKRoundRect(new SKRect(20, 20, width - 20, height - 20), 25);
            canvas.DrawRoundRect(rect, panelPaint);
        }

        // --- AVATAR ---
        var avatarUrl = user.GetAvatarUrl(size: 256) ?? user.GetDefaultAvatarUrl();

        SKBitmap avatarBitmap = null;
        try
        {
            using var avatarStream = await Http.GetStreamAsync(avatarUrl);
            using var avatarData = SKData.Create(avatarStream);
            avatarBitmap = SKBitmap.Decode(avatarData);
        }
        catch
        {
            // ignoruj błąd, najwyżej avatar == null
        }

        const int avatarSize = 180;
        var avatarX = 40;
        var avatarY = (height - avatarSize) / 2;

        if (avatarBitmap != null)
        {
            using var avatarScaled = avatarBitmap.Resize(
                new SKImageInfo(avatarSize, avatarSize),
                SKFilterQuality.High
            );

            var avatarRect = new SKRect(avatarX, avatarY, avatarX + avatarSize, avatarY + avatarSize);

            // glow
            using (var glowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(140, 0, 220, 160),
                ImageFilter = SKImageFilter.CreateBlur(18, 18)
            })
            {
                canvas.DrawOval(avatarRect, glowPaint);
            }

            // maska koła
            using var avatarPaint = new SKPaint { IsAntialias = true };
            var path = new SKPath();
            path.AddOval(avatarRect);

            canvas.Save();
            canvas.ClipPath(path);
            canvas.DrawBitmap(avatarScaled, avatarRect);
            canvas.Restore();

            // obramówka
            using var borderPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(255, 255, 255, 200),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4
            };
            canvas.DrawOval(avatarRect, borderPaint);
        }

        // --- FONTY ---
        var usernamePaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255),
            TextSize = 36,
            Typeface = SKTypeface.FromFamilyName("Montserrat", SKFontStyle.Bold)
        };

        var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(200, 200, 200),
            TextSize = 22,
            Typeface = SKTypeface.FromFamilyName("Montserrat", SKFontStyle.Normal)
        };

        var valuePaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 215, 0),
            TextSize = 24,
            Typeface = SKTypeface.FromFamilyName("Montserrat", SKFontStyle.Bold)
        };

        var smallPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(220, 220, 220),
            TextSize = 18,
            Typeface = SKTypeface.FromFamilyName("Montserrat", SKFontStyle.Italic)
        };

        int textBaseX = avatarX + avatarSize + 40;
        int lineY = 70;

        // Username
        var username = user.Username;
        canvas.DrawText(username, textBaseX, lineY, usernamePaint);
        lineY += 40;

        // ID / tag
        canvas.DrawText($"ID: {user.Id}", textBaseX, lineY, smallPaint);
        lineY += 40;

        // Credits
        canvas.DrawText("Credits:", textBaseX, lineY, labelPaint);
        canvas.DrawText(data.Credits.ToString(), textBaseX + 130, lineY, valuePaint);
        lineY += 30;

        // Level
        canvas.DrawText("Level:", textBaseX, lineY, labelPaint);
        canvas.DrawText(data.Level.ToString(), textBaseX + 130, lineY, valuePaint);
        lineY += 30;

        // Streak
        canvas.DrawText("Daily streak:", textBaseX, lineY, labelPaint);
        canvas.DrawText($"{data.Streak} 🔥", textBaseX + 170, lineY, valuePaint);
        lineY += 40;

        // --- EXP BAR ---
        int barWidth = 380;
        int barHeight = 26;
        int barX = textBaseX;
        int barY = lineY;

        int maxExp = Math.Max(1, data.Level * 100);
        float progress = Math.Clamp((float)data.Exp / maxExp, 0f, 1f);

        // pasek tła
        using (var barBgPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(25, 25, 40)
        })
        {
            var barBgRect = new SKRoundRect(new SKRect(barX, barY, barX + barWidth, barY + barHeight), 13);
            canvas.DrawRoundRect(barBgRect, barBgPaint);
        }

        // pasek wypełnienia
        using (var barFgPaint = new SKPaint { IsAntialias = true })
        {
            barFgPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(barX, barY),
                new SKPoint(barX + barWidth, barY),
                new[]
                {
                    new SKColor(0, 255, 200),
                    new SKColor(140, 0, 220)
                },
                null,
                SKShaderTileMode.Clamp
            );

            var barFgRect = new SKRoundRect(
                new SKRect(barX, barY, barX + barWidth * progress, barY + barHeight),
                13
            );

            canvas.DrawRoundRect(barFgRect, barFgPaint);
        }

        using (var barTextPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255),
            TextSize = 18
        })
        {
            string expText = $"{data.Exp} / {maxExp} EXP";
            var textBounds = new SKRect();
            barTextPaint.MeasureText(expText, ref textBounds);
            float textX = barX + (barWidth - textBounds.Width) / 2;
            float textY = barY + barHeight / 2 + textBounds.Height / 2 - 3;

            canvas.DrawText(expText, textX, textY, barTextPaint);
        }

        // --- BIO ---
        int bioX = textBaseX;
        int bioY = barY + barHeight + 40;

        string bio = string.IsNullOrWhiteSpace(data.Bio) ? "Brak opisu." : data.Bio;
        if (bio.Length > 80) bio = bio.Substring(0, 77) + "...";

        canvas.DrawText("Bio:", bioX, bioY, labelPaint);
        canvas.DrawText(bio, bioX + 60, bioY, smallPaint);

        // --- EXPORT PNG ---
        using var image = surface.Snapshot();
        using var dataPng = image.Encode(SKEncodedImageFormat.Png, 100);
        return dataPng.ToArray();
    }
}
