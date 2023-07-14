using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    [HarmonyPatch("DSharpPlus.VoiceNext.Entities.VoiceStateUpdatePayload", "Deafened", MethodType.Setter)]
    internal class DeafenHook
    {
        internal static bool isDeafen = true;
        static bool Prefix(ref bool value)
        {
            value = isDeafen;
            return true;
        }
    }
}
