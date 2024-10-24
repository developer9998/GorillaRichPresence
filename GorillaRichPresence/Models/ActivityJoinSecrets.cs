namespace GorillaRichPresence.Models
{
    public class ActivityJoinSecrets
    {
        public string Secrets;

        public readonly string RoomName, PlayerId;

        public ActivityJoinSecrets(string secrets)
        {
            Secrets = secrets;

            object[] secretContents = Secrets.Split("\n");
            RoomName = ((string)secretContents[0]).Trim();
            PlayerId = ((string)secretContents[1]).Trim();
        }
    }
}
