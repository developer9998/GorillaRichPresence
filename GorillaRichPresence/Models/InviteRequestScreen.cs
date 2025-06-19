using Discord;
using GorillaInfoWatch.Models;
using GorillaInfoWatch.Models.Widgets;
using GorillaInfoWatch.Screens;
using System;
using System.Linq;

namespace GorillaRichPresence.Models
{
    internal class InviteRequestScreen : InfoWatchScreen
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
                ReturnToHomePage();
                return null;
            }

            LineBuilder lines = new();

            lines.Add("A user has invited you to play with them:");
            lines.Add(requestingUser.Username, new Widget_Symbol((Symbol)requestingAvatar));

            lines.Skip();
            lines.Add("Would you like to join this user?");
            lines.Add("Accept", new Widget_PushButton(ReplyChosen, true));
            lines.Add("Decline", new Widget_PushButton(ReplyChosen, false));

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
