﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

/* Keep the following list up to date:
 * Currently defined Generics:
 * -for Meals - Pawns auto fetch a best food, generaly meals
 * -for Raw Foodstuff - Pawns can auto fetch raw food if no meals, also will fetch for animal training.
 * -for Drugs - Pawns can auto fetch drugs to fit a schedule.
 */
 // TODO Map generic type to ThingRequestGroup so to get rid of querying ListerThing.AllThings
namespace RPG_Inventory_Remake.Loadout
{
	/// <summary>
	/// LoadoutGenericDef handles Generic LoadoutSlots.
	/// </summary>
	[StaticConstructorOnStartup]
	public class LoadoutGenericDef : ThingDef
	{
		#region Fields
		public LoadoutCountType defaultCountType = LoadoutCountType.dropExcess; // default: drop anything more than (default)Count.
		public int defaultCount = 1;
		private Predicate<ThingDef> _lambda = td => true;
		public ThingRequestGroup thingRequestGroup = ThingRequestGroup.HaulableEver;
		public bool isBasic = false;

		// The following group are intentionally left unsaved so that if the game state changes between saves the new value is calculated.
		private float _mass;
		private bool _cachedVars = false;
		private static List<LoadoutGenericDef> _genericDefs = new List<LoadoutGenericDef>();

		#endregion

		#region Constructors
		//UNDONE This doesn't define weapons as yet and the code might not handle that well.  Want to get various things stable first RE inventory.
		//       But we can define generics for short range, assault, pistol, melee.
		/*       (ProfoundDarkness) Some issues with weapons is that they have durability, quality, and often made of stuffs.
		 *                          Also could use a super-generic which fetches x clips for each weapon on the pawn (working on that for something else).
		 *                          I'm thinking we could add another button (more clutter) to each loadout slot which is only displayed if the item
		 *                           has key properties.  Clicking that button would show a new window which lets the user configure parameters like
		 *                           a range slider for durability, range slider for quality, and a checklist for stuffs (assuming is made of stuffs).
		 */

		/// <summary>
		/// This constructor gets run on startup of RimWorld and generates the various LoadoutGenericDef instance objects akin to having been loaded from xml.
		/// </summary>
		static LoadoutGenericDef()
		{
			// Used in a handful of places where all loaded ThingDefs are useful.
			IEnumerable<ThingDef> everything = DefDatabase<ThingDef>.AllDefs;

			// need to generate a list as that's how new defs are taken by DefDatabase.


			LoadoutGenericDef generic = new LoadoutGenericDef();
			generic.defName = "GenericMeal";
			generic.description = "Generic Loadout for perishable meals.  Intended for compatibility with pawns automatically picking up a meal for themself.";
			generic.label = "Corgi_Generic_Meal".Translate();
			generic.defaultCountType = LoadoutCountType.pickupDrop; // Fits with disabling of RimWorld Pawn behavior of fetching meals themselves.
			generic._lambda = td => td.IsNutritionGivingIngestible && td.ingestible.preferability >= FoodPreferability.MealAwful && td.GetCompProperties<CompProperties_Rottable>()?.daysToRotStart <= 5 && !td.IsDrug;
			generic.isBasic = true;

			_genericDefs.Add(generic);
			//Log.Message(string.Concat("CombatExtended :: LoadoutGenericDef :: ", generic.LabelCap, " list: ", string.Join(", ", DefDatabase<ThingDef>.AllDefs.Where(t => generic.lambda(t)).Select(t => t.label).ToArray())));


			float targetNutrition = 0.85f;
			generic = new LoadoutGenericDef();
			generic.defName = "GenericRawFood";
			generic.description = "Generic Loadout for Raw Food.  Intended for compatibility with pawns automatically picking up raw food to train animals.";
			generic.label = "Corgi_Generic_RawFood".Translate();
			// Exclude drugs and corpses.  Also exclude any food worse than RawBad as in testing the pawns would not even pick it up for training.
			generic._lambda = td => td.IsNutritionGivingIngestible && td.ingestible.preferability <= FoodPreferability.RawTasty && td.ingestible.HumanEdible && td.plant == null && !td.IsDrug && !td.IsCorpse;
			generic.defaultCount = Convert.ToInt32(Math.Floor(targetNutrition / everything.Where(td => generic.lambda(td)).Average(td => td.ingestible.CachedNutrition)));
			//generic.defaultCount = 1;
			generic.isBasic = false; // doesn't need to be in loadouts by default as animal interaction talks to HoldTracker now.
									 //TODO: Test pawns fetching raw food if no meal is available, if so then add a patch to have that talk to HoldTracker too.

			_genericDefs.Add(generic);
			//Log.Message(string.Concat("CombatExtended :: LoadoutGenericDef :: ", generic.LabelCap, " list: ", string.Join(", ", DefDatabase<ThingDef>.AllDefs.Where(t => generic.lambda(t)).Select(t => t.label + " B(" + t.GetStatValueAbstract(CE_StatDefOf.Bulk) + ") M(" + t.GetStatValueAbstract(StatDefOf.Mass) + ")").ToArray())));


			generic = new LoadoutGenericDef();
			generic.defName = "GenericDrugs";
			generic.defaultCount = 3;
			generic.description = "Generic Loadout for Drugs.  Intended for compatibility with pawns automatically picking up drugs in compliance with drug policies.";
			generic.label = "Corgi_Generic_Drugs".Translate();
			generic.thingRequestGroup = ThingRequestGroup.Drug;
			generic.isBasic = true;

			_genericDefs.Add(generic);
			//Log.Message(string.Concat("CombatExtended :: LoadoutGenericDef :: ", generic.LabelCap, " list: ", string.Join(", ", DefDatabase<ThingDef>.AllDefs.Where(t => generic.lambda(t)).Select(t => t.label).ToArray())));


			generic = new LoadoutGenericDef();
			generic.defName = "GenericMedicine";
			generic.defaultCount = 5;
			generic.defaultCountType = LoadoutCountType.pickupDrop;
			generic.description = "Generic Loadout for Medicine.  Intended for pawns which will handle triage activities.";
			generic.label = "Corgi_Generic_Medicine".Translate();
			generic.thingRequestGroup = ThingRequestGroup.Medicine;

			_genericDefs.Add(generic);

			// finally we add all the defs generated to the DefDatabase.
			DefDatabase<LoadoutGenericDef>.Add(_genericDefs);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Property gets/runs the lambda defining what ThingDefs are accepted by this def.
		/// </summary>
		public Predicate<ThingDef> lambda { get { return _lambda; } }

		/// <summary>
		/// Property gets the calculated mass of this def.  This is determined at runtime based on stored Lambda rather than a static value.
		/// </summary>
		public float mass
		{
			get
			{
				if (!_cachedVars)
					updateVars();
				return _mass;
			}
		}

		/// <summary>
		/// Generic defs that are added to DefDatabase
		/// </summary>
		public static List<LoadoutGenericDef> GenericDefs
		{
			get => _genericDefs;
		}

		public static List<ThingDef> GenericDefsToThingDefs
		{
			get
			{
				return _genericDefs.Select(def => (ThingDef)def).ToList();
			}
		}
		#endregion Properties

		#region Methods
		/// <summary>
		/// Handles updating this def's stored mass values.
		/// </summary>
		/// <remarks>Can be a bit expensive but only done once per def the first time such values are requested.</remarks>
		private void updateVars()
		{
			IEnumerable<ThingDef> matches;
			matches = DefDatabase<ThingDef>.AllDefs.Where(td => lambda(td) && thingRequestGroup.Includes(td));
			_mass = matches.Max(t => t.GetStatValueAbstract(StatDefOf.Mass));
			_cachedVars = true;
		}

		#endregion Methods
	}
}
