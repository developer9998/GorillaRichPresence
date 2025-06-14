using Discord;
using GorillaInfoWatch.Models;
using GorillaInfoWatch.Models.Widgets;
using GorillaInfoWatch.Screens;
using System;
using System.Linq;

namespace GorillaRichPresence.Models
{
    internal class JoinRequestScreen : Screen
    {
        public override string Title => "Join Request";

        public static bool hasUser;

        public static User requestingUser;

        public static UnityEngine.Texture requestingAvatar;

        public static Action<User, ActivityJoinRequestReply> sendReply;

        public override ScreenContent GetContent()
        {
            if (!hasUser)
            {
                SetScreen(CallerType);
                return null;
            }

            LineBuilder lines = new();

            lines.AddLine("A user has requested to join you:");
            lines.AddLine(requestingUser.Username, new WidgetSymbol((Symbol)requestingAvatar));

            lines.AddLine();
            lines.AddLine("Select a specified reply:");
            lines.AddLine("Accept", new PushButton(ReplyChosen, ActivityJoinRequestReply.Yes));
            lines.AddLine("Decline", new PushButton(ReplyChosen, ActivityJoinRequestReply.No));
            lines.AddLine("Ignore", new PushButton(ReplyChosen, ActivityJoinRequestReply.Ignore));

            return lines;
        }

        public void ReplyChosen(object[] args)
        {
            if (args.ElementAtOrDefault(0) is ActivityJoinRequestReply reply)
                ReplyChosen(reply);
        }

        public void ReplyChosen(ActivityJoinRequestReply reply)
        {
            if (hasUser)
            {
                sendReply?.Invoke(requestingUser, reply);
                hasUser = false;
                SetScreen<HomeScreen>();
            }
        }
    }
}
