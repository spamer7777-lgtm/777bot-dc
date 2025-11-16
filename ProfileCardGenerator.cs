using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public static class ProfileCardGenerator
{
    public static async Task<byte[]> GenerateAsync(SocketUser user, UserData data)
    {
        int width = 800;
        int height = 250;

        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(28, 28, 30)); // dark background

        // --- Avatar ---
        var avatarUrl = user.GetAvatarUrl(ImageFormat.Png, 256) 
                        ?? user.GetDefaultAvatarUrl();

        using var avatarStream = await new HttpClient().GetStreamAsync(avatarUrl);
        using var avatarImg = Image.FromStream(avatarStream);

        Rectangle avatarRect = new(20, 20, 200, 200);
        g.DrawImage(avatarImg, avatarRect);

        // --- Username ---
        using var fontUser = new Font("Arial", 32, FontStyle.Bold);
        g.DrawString(user.Username, fontUser, Brushes.White, 240, 20);

        // --- Credits ---
        using var fontCredits = new Font("Arial", 24, FontStyle.Bold);
        g.DrawString($"Credits: {data.Credits}", fontCredits, Brushes.Gold, 240, 70);

        // --- Level ---
        g.DrawString($"Level: {data.Level}", fontCredits, Brushes.LightSkyBlue, 240, 110);

        // --- EXP bar ---
        int maxExp = data.Level * 100;
        float percentage = (float)data.Exp / maxExp;

        var barBg = new Rectangle(240, 160, 500, 30);
        var barFg = new Rectangle(240, 160, (int)(500 * percentage), 30);

        g.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 50)), barBg);
        g.FillRectangle(Brushes.LimeGreen, barFg);

        g.DrawString($"{data.Exp} / {maxExp} EXP", new Font("Arial", 16), Brushes.White, 240, 195);

        // --- Save ---
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
