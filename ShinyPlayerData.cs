using System;
using System.Collections.Generic;
using RWCustom;
using Smoke;
using UnityEngine;

namespace DoubleJump
{
	// Token: 0x02000022 RID: 34
	public class ShinyPlayerData
	{
		// Token: 0x06000121 RID: 289 RVA: 0x00017CCC File Offset: 0x00015ECC
		public ShinyPlayerData(Player player)
		{
			this.IsCustomShiny = !player.isSlugpup;
			this.playerRef = new WeakReference<Player>(player);
		}

		public readonly bool IsCustomShiny;

		public WeakReference<Player> playerRef;
        public bool hasDoubleJump;
		public bool canWallDoubleJump;
		public bool crouchJumped;
    }
}
