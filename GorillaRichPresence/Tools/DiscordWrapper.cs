using BepInEx;
using Discord;
using GorillaInfoWatch;
using GorillaInfoWatch.Models;
using GorillaNetworking;
using GorillaRichPresence.Models;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace GorillaRichPresence.Tools
{
    public static class DiscordWrapper
    {
        public static event Action<string> OnActivityJoin;

        private static Discord.Discord discord;
        private static ActivityManager activityManager;
        private static Activity activity;

        private static bool toUpload = true;
        private static float lastUploadTime = Constants.ActivityUploadDelay;

        private static Thread thread;

        public static void Initialize()
        {
            if (thread is not null) return;

            thread = new Thread(RegisterDiscord);
            thread.Start();
        }

        public static void RegisterDiscord()
        {
            if (discord != null)
            {
                Logging.Error("RegisterDiscord was cancelled as a Discord instance is already in use.");
                return;
            }

            discord = new Discord.Discord(Constants.ApplicationID, (ulong)CreateFlags.NoRequireDiscord);

            discord.SetLogHook(LogLevel.Info, (level, message) =>
            {
                Logging.Log($"Discord: {message}", level switch
                {
                    LogLevel.Warn => BepInEx.Logging.LogLevel.Warning,
                    _ => Enum.Parse<BepInEx.Logging.LogLevel>(level.GetName())
                });
            });

            activityManager = discord.GetActivityManager();

            if (activityManager == null)
            {
                Logging.Error("RegisterDiscord was interrupted as an ActivityManager is required for rich presence.");
                return;
            }

            activityManager.RegisterSteam(Constants.SteamID);

            activity.Timestamps.Start = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();

            // user joins you
            activityManager.OnActivityJoin += (string secret) =>
            {
                Logging.Info($"OnActivityJoin {secret}");
                OnActivityJoin?.Invoke(secret);
            };

            // user asks whether they can join you
            activityManager.OnActivityJoinRequest += (ref User user) =>
            {
                User requestingUser = user;

                Logging.Info($"OnActivityJoinRequest {JsonUtility.ToJson(user, true)}");
                ThreadingHelper.Instance.StartSyncInvoke(async () =>
                {
                    string avatarUrl = string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png?size=128", requestingUser.Id, requestingUser.Avatar);
                    UnityWebRequest request = UnityWebRequestTexture.GetTexture(avatarUrl);
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    await operation;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture tex = ((DownloadHandlerTexture)request.downloadHandler).texture;

                        void ReplyChosen(User user, ActivityJoinRequestReply reply)
                        {
                            if (user.Id == requestingUser.Id)
                            {
                                JoinRequestScreen.sendReply -= ReplyChosen;
                                activityManager.SendRequestReply(user.Id, reply, (res) =>
                                {
                                    if (res == Result.Ok)
                                    {
                                        Logging.Info("Responded successfully");
                                    }
                                });
                            }
                        }

                        Events.SendNotification(new("You received a join request", requestingUser.Username, 5, InfoWatchSound.notificationPositive, new(typeof(JoinRequestScreen), "Invite", delegate ()
                        {
                            JoinRequestScreen.hasUser = true;
                            JoinRequestScreen.requestingUser = requestingUser;
                            JoinRequestScreen.requestingAvatar = tex;
                            JoinRequestScreen.sendReply += ReplyChosen;
                        })));
                    }
                });
            };

            // invited to play by another user
            activityManager.OnActivityInvite += (ActivityActionType Type, ref User user, ref Activity invitedActivity) =>
            {
                User requestingUser = user;

                Logging.Info($"OnActivityInvite {Type} {JsonUtility.ToJson(user, true)} {invitedActivity.Name}");
                ThreadingHelper.Instance.StartSyncInvoke(async () =>
                {
                    string avatarUrl = string.Format("https://cdn.discordapp.com/avatars/{0}/{1}.png?size=128", requestingUser.Id, requestingUser.Avatar);
                    UnityWebRequest request = UnityWebRequestTexture.GetTexture(avatarUrl);
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    await operation;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture tex = ((DownloadHandlerTexture)request.downloadHandler).texture;

                        void ReplyChosen(User user, bool accept)
                        {
                            if (user.Id == requestingUser.Id)
                            {
                                InviteRequestScreen.sendReply -= ReplyChosen;
                                if (accept)
                                {
                                    activityManager.AcceptInvite(user.Id, result =>
                                    {
                                        Logging.Info($"AcceptInvite {result}");
                                    });
                                }
                            }
                        }

                        Events.SendNotification(new("You received an invite", requestingUser.Username, 5, InfoWatchSound.notificationPositive, new(typeof(InviteRequestScreen), "Invite", delegate ()
                        {
                            InviteRequestScreen.hasUser = true;
                            InviteRequestScreen.requestingUser = requestingUser;
                            InviteRequestScreen.requestingAvatar = tex;
                            InviteRequestScreen.sendReply += ReplyChosen;
                        })));
                    }
                });
            };

            try
            {
                while (true)
                {
                    bool hasException = false;

                    try
                    {
                        Thread.Sleep((int)(Constants.TickDebounce * 1000f));

                        if (toUpload && (Time.realtimeSinceStartup > (lastUploadTime + Constants.ActivityUploadDelay)))
                        {
                            toUpload = false;
                            lastUploadTime = Time.realtimeSinceStartup;

                            try
                            {
                                activityManager.UpdateActivity(activity, result => { });
                            }
                            catch (Exception ex)
                            {
                                Logging.Fatal("ActivityManager could not update");
                                Logging.Error(ex);
                            }
                        }

                        discord.RunCallbacks();
                    }
                    catch (ResultException ex)
                    {
                        Logging.Fatal("Discord threw a ResultException");
                        Logging.Error(ex);
                    }

                    if (hasException) break;
                }
            }
            finally
            {
                Logging.Info("Disposing of our Discord instance");

                discord.Dispose();
                discord = null;
            }
        }

        public static void SetActivity(Func<Activity, Activity> modificationFunc)
        {
            Activity modifiedActivity = modificationFunc(activity);
            activity = modifiedActivity;
        }

        public static void UpdateActivity(bool updateSecrets = true)
        {
            if (updateSecrets)
            {
                string joinSecret = "";

                if (NetworkSystem.Instance.InRoom)
                {
                    List<string> list = new();

                    list.Add(NetworkSystem.Instance.RoomName);

                    if (NetworkSystem.Instance.SessionIsPrivate)
                        list.Add(PhotonNetworkController.Instance.privateTrigger.networkZone);
                    else
                    {
                        AuthenticationValues authenticationValues = NetworkSystem.Instance.GetAuthenticationValues();
                        if (authenticationValues is not null && authenticationValues.AuthPostData is Dictionary<string, object> dictionary && dictionary.TryGetValue("Zone", out object zone))
                            list.Add(zone.ToString());
                        else
                            list.Add("none");
                    }

                    joinSecret = string.Join("\n", list);
                    Logging.Info(joinSecret);
                }

                if (activity.Secrets.Join != joinSecret)
                {
                    Logging.Info($"Secret: {string.Join(", ", joinSecret.Split("\n"))}");
                    activity.Secrets.Join = joinSecret;
                }

                activity.Secrets.Match = string.IsNullOrEmpty(joinSecret) ? "" : "foo match";
                activity.Secrets.Spectate = string.IsNullOrEmpty(joinSecret) ? "" : "foo spectate";
            }

            toUpload = true;
        }
    }
}
