using Discord;
using System;
using System.Threading;
using UnityEngine;

namespace GorillaRichPresence.Tools
{
    public class DiscordRegistrar
    {
        private Discord.Discord _discord;
        private ActivityManager _activityManager;
        private Activity _activity;

        private bool _updateRequested = true;
        private float _lastUpdate;

        public void Initialize()
        {
            new Thread(RegisterDiscord).Start();
        }

        public void RegisterDiscord()
        {
            if (_discord != null)
            {
                Logging.Error("RegisterDiscord was cancelled as a Discord instance is already in use.");
                return;
            }

            _discord = new Discord.Discord(RP_Constants.ApplicationID, (ulong)CreateFlags.NoRequireDiscord);
            _activityManager = _discord.GetActivityManager();

            if (_activityManager == null)
            {
                Logging.Error("RegisterDiscord was interrupted as an ActivityManager is required for rich presence.");
                return;
            }

            _activityManager.RegisterSteam(RP_Constants.SteamID);

            _activity.Timestamps.Start = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();

            _activityManager.OnActivityJoinRequest += (ref User user) =>
            {
                Console.WriteLine("Join request from: {0}", user.Id);
                _activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, (res) =>
                {
                    if (res == Result.Ok)
                    {
                        Console.WriteLine("Responded successfully");
                    }
                });
            };

            _activityManager.OnActivityInvite += (ActivityActionType Type, ref User user, ref Activity activity2) =>
            {
                Console.WriteLine("Received Invite Type: {0}, from User: {1}, with Activity Name: {2}", Type, user.Username, activity2.Name);
                _activityManager.AcceptInvite(user.Id, result =>
                {
                    Console.WriteLine("AcceptInvite {0}", result);
                });
            };

            _activityManager.OnActivityJoin += secret =>
            {
                Console.WriteLine("OnJoin {0}", secret);
            };

            try
            {
                while (true)
                {
                    try
                    {
                        Thread.Sleep((int)(RP_Constants.TickDebounce * 1000f));

                        if (_updateRequested && (Time.realtimeSinceStartup > (_lastUpdate + RP_Constants.UpdateDebunce) || _lastUpdate == 0))
                        {
                            _updateRequested = false;
                            _lastUpdate = Time.realtimeSinceStartup;

                            try
                            {
                                _activityManager.UpdateActivity(_activity, result => { });
                            }
                            catch (Exception ex)
                            {
                                Logging.Error(string.Concat("ActivityManager could not be updated: ", ex));
                            }
                        }

                        _discord.RunCallbacks();
                    }
                    catch (ResultException ex)
                    {
                        Logging.Error(string.Concat("Discord threw a ResultException: ", ex.Message));
                    }
                }
            }
            finally
            {
                Logging.Info("Disposing of our Discord instance");

                _discord.Dispose();
                _discord = null;
            }
        }

        public void ModifyActivity(Func<Activity, Activity> modificationFunc)
        {
            Activity modifiedActivity = modificationFunc(_activity);
            _activity = modifiedActivity;
        }

        public void UpdateActivity()
        {
            _updateRequested = true;
        }
    }
}
