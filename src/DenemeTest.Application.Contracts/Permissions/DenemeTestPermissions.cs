namespace DenemeTest.Permissions;

public static class DenemeTestPermissions
{
    public const string GroupName = "DenemeTest";

    public static class Exams
    {
        public const string Default = GroupName + ".Exams";
        public const string Reports = Default + ".Reports";
        public const string Questions = Default + ".Questions";
        public const string Candidates = Default + ".Candidates";
        public const string Invitations = Default + ".Invitations";
        public const string Tests = Default + ".Tests";
    }
}
