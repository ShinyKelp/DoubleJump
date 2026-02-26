using System;
using System.Runtime.CompilerServices;
using ImprovedInput;
using UnityEngine;

namespace DoubleJump
{
	// Token: 0x02000023 RID: 35
	public static class PlayerExtension
	{
		public static ShinyPlayerData Shiny(this Player player)
		{
			return PlayerExtension.cwt.GetValue(player, (Player _) => new ShinyPlayerData(player));
		}

		public static Player Get(this WeakReference<Player> weakRef)
		{
			Player result;
			weakRef.TryGetTarget(out result);
			return result;
		}


		public static bool IsShiny(this Player player)
		{
			return player.Shiny().IsCustomShiny;
		}

		public static bool IsShiny(this Player player, out ShinyPlayerData Shiny)
		{
			Shiny = player.Shiny();
			return Shiny.IsCustomShiny;
		}

		private static readonly ConditionalWeakTable<Player, ShinyPlayerData> cwt = new ConditionalWeakTable<Player, ShinyPlayerData>();
	}
}
