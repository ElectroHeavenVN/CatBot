using DSharpPlus;
using DSharpPlus.Entities;
using HarmonyLib;

namespace DiscordBot
{
    [HarmonyPatch("DSharpPlus.Utilities", "IsTextableChannel")]
    internal class IsTextableChannelHook
    {
        static bool Prefix(DiscordChannel channel, ref bool __result)
        {
            if (channel.Type == ChannelType.Stage)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
