using Discord;
using GorillaInfoWatch.Models;
using GorillaInfoWatch.Models.Widgets;
using GorillaInfoWatch.Screens;
using System;
using System.Linq;

namespace GorillaRichPresence.Models
{
    internal class JoinRequestScreen : InfoWatchScreen
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
                ReturnToHomePage();
                return null;
            }

            LineBuilder lines = new();

            lines.Add("A user has requested to join you:");
            lines.Add(requestingUser.Username, new Widget_Symbol((Symbol)requestingAvatar));

            lines.Skip();
            lines.Add("Select a specified reply:");
            lines.Add("Accept", new Widget_PushButton(ReplyChosen, ActivityJoinRequestReply.Yes));
            lines.Add("Decline", new Widget_PushButton(ReplyChosen, ActivityJoinRequestReply.No));
            lines.Add("Ignore", new Widget_PushButton(ReplyChosen, ActivityJoinRequestReply.Ignore));

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
