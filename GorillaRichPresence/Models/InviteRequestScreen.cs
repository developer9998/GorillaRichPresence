using Discord;
using GorillaInfoWatch.Models;
using GorillaInfoWatch.Models.Widgets;
using GorillaInfoWatch.Screens;
using System;
using System.Linq;

namespace GorillaRichPresence.Models
{
    internal class InviteRequestScreen : Screen
    {
        public override string Title => "Join Invite";

        public static bool hasUser;

        public static User requestingUser;

        public static UnityEngine.Texture requestingAvatar;

        public static Action<User, bool> sendReply;

        public override ScreenContent GetContent()
        {
            if (!hasUser)
            {
                SetScreen(CallerType);
                return null;
            }

            LineBuilder lines = new();

            lines.AddLine("A user has invited you to play with them:");
            lines.AddLine(requestingUser.Username, new WidgetSymbol((Symbol)requestingAvatar));

            lines.AddLine();
            lines.AddLine("Select a specified reply:");
            lines.AddLine("Accept", new PushButton(ReplyChosen, true));
            lines.AddLine("Decline", new PushButton(ReplyChosen, false));

            return lines;
        }

        public void ReplyChosen(object[] args)
        {
            if (args.ElementAtOrDefault(0) is bool accept)
                ReplyChosen(accept);
        }

        public void ReplyChosen(bool accept)
        {
            if (hasUser)
            {
                sendReply?.Invoke(requestingUser, accept);
                hasUser = false;
                SetScreen<HomeScreen>();
            }
        }
    }
}
