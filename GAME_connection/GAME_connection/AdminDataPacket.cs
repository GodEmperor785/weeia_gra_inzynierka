﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GAME_connection {
	[Serializable]
	public class AdminDataPacket {
		private List<Ship> shipTemplates;
		private List<Weapon> weapons;
		private List<DefenceSystem> defences;
		private BaseModifiers baseModifiers;
		private List<Faction> factions;
		private List<LootBox> lootboxes;

		public AdminDataPacket() {
			shipTemplates = new List<Ship>();
			weapons = new List<Weapon>();
			defences = new List<DefenceSystem>();
			factions = new List<Faction>();
			lootboxes = new List<LootBox>();
		}

		public AdminDataPacket(List<Ship> shipTemplates, List<Weapon> weapons, List<DefenceSystem> defences, BaseModifiers baseModifiers, List<Faction> factions, List<LootBox> lootboxes) {
			this.shipTemplates = shipTemplates;
			this.weapons = weapons;
			this.defences = defences;
			this.baseModifiers = baseModifiers;
			this.factions = factions;
			this.lootboxes = lootboxes;
		}

		public List<Ship> ShipTemplates { get => shipTemplates; set => shipTemplates = value; }
		public List<Weapon> Weapons { get => weapons; set => weapons = value; }
		public List<DefenceSystem> Defences { get => defences; set => defences = value; }
		public BaseModifiers BaseModifiers { get => baseModifiers; set => baseModifiers = value; }
		public List<Faction> Factions { get => factions; set => factions = value; }
		public List<LootBox> Lootboxes { get => lootboxes; set => lootboxes = value; }
	}
}
