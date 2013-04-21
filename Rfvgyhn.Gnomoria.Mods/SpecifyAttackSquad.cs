using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Game;
using Game.GUI;
using Game.GUI.Controls;

namespace Rfvgyhn.Gnomoria.Mods
{
    public class SpecifyAttackSquad : Mod
    {
        static Military military = new Military();
        static FieldInfo panelTarget;
        static long militaryPosition;

        public override string Name
        {
            get
            {
                return "Specify Attack Squad";
            }
        }

        public override string Description
        {
            get
            {
                return "Allows you to specify a squad when attacking.";
            }
        }

        public override Version Version
        {
            get
            {
                return new Version(1, 2);
            }
        }

        public override IMethodModification[] Hooks
        {
            get
            {
                return new IMethodModification[]
			    {
                    new MethodHook(typeof(BlueprintManager).GetConstructor(new Type[] { typeof(BinaryReader) }), Method.Of(new Action<BlueprintManager, BinaryReader>(BeforeLoadTargets)), MethodHookType.RunAfter, MethodHookFlags.None),
                    new MethodHook(typeof(Game.Military).GetConstructor(new Type[] { typeof(BinaryReader) }), Method.Of(new Action<Game.Military, BinaryReader>(LoadTargets)), MethodHookType.RunAfter, MethodHookFlags.None),
                    new MethodHook(typeof(Game.Military).GetMethod("FindAttackTarget"), Method.Of(new Func<Game.Military, Character, Character>(FindAttackTarget)), MethodHookType.Replace, MethodHookFlags.None),
                    new MethodHook(typeof(Game.Military).GetMethod("RemoveAttackTarget"), Method.Of(new Action<Game.Military, Character>(RemoveAttackTarget)), MethodHookType.RunAfter, MethodHookFlags.None),
                    new MethodHook(typeof(Squad).GetMethod("Disband"), Method.Of(new Action<Squad>(Disband)), MethodHookType.RunAfter, MethodHookFlags.None),
                    new MethodHook(typeof(CharacterOverviewUI).GetMethod("SetupPanel"), Method.Of(new Action<CharacterOverviewUI>(SetupPanel)), MethodHookType.RunAfter, MethodHookFlags.None)
			    };
            }
        }

        public override void Initialize_PreGame()
        {
            base.Initialize_PreGame();

            panelTarget = typeof(CharacterOverviewUI).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(f => f.FieldType == typeof(Character));
            
        }

        /// <summary>
        /// Get save position of the military object
        /// </summary>
        public static void BeforeLoadTargets(BlueprintManager m, BinaryReader reader)
        {
            militaryPosition = reader.BaseStream.Position;
        }

        /// <summary>
        /// Adds attack targets from save file
        /// </summary>
        /// <param name="m"></param>
        /// <param name="reader"></param>
        public static void LoadTargets(Game.Military m, BinaryReader reader)
        {
            // Need to get attack targets only. Since reflection can't differentiate between attack and defend targets without using the
            // field name (field name may change between releases since it's obfuscated), read them from the save file
            var readerPosition = reader.BaseStream.Position;
            reader.BaseStream.Seek(militaryPosition, SeekOrigin.Begin);

            int num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
                new Uniform(reader);

            num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
                new SquadPosition(reader, m);

            num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
                new Formation(reader, m);

            num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
                new GuardStation(reader);

            num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
                ReadPatrolRoute(reader);    // PatrolRoute constructor modifies the map object, so manually progress the reader position

            if (GnomanEmpire.Instance.LoadingSaveVersion >= 4)
            {
                num = reader.ReadInt32();
                for (int i = 0; i < num; i++)
                    ReadTrainingStation(reader);    // TrainingStation constructor modifies the map object, so manually progress the reader position
            }

            GameEntityManager entityManager = GnomanEmpire.Instance.EntityManager;
            num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
            {
                Character character = entityManager.Entity(reader.ReadUInt32()) as Character;

                if (character != null)
                    military.AddTarget(character);
            }

            reader.BaseStream.Seek(readerPosition, SeekOrigin.Begin);
        }

        /// <summary>
        /// Override Game.Military.FindAttackTarget
        /// </summary>
        public static Character FindAttackTarget(Game.Military m, Character c)
        {
            return military.FindAttackTarget(c);
        }

        /// <summary>
        /// Remove attack targer Game.Military.RemoveAttackTarget is called
        /// </summary>
        public static void RemoveAttackTarget(Game.Military m, Character c)
        {
            military.RemoveAttackTarget(c);
        }

        /// <summary>
        /// Add custom attack button and squad list
        /// </summary>
        public static void SetupPanel(CharacterOverviewUI panel)
        {
            const string AttackLbl = "Attack";
            const string MoveToLbl = "Move To";
            const string ListItemFormat = "{0} ({1})";

            var controls = panel.Controls.Where(c => c is ClipBox).Single().Controls;
            var oldAttackBtn = controls.Where(c => c.Text == AttackLbl).SingleOrDefault();

            if (oldAttackBtn == null)
                return;

            panel.Remove(oldAttackBtn);
            oldAttackBtn = null;

            var target = (Character)panelTarget.GetValue(panel);
            var moveToBtn = controls.Where(c => c.Text == MoveToLbl).SingleOrDefault();
            var squadNames = GnomanEmpire.Instance.Fortress.Military.Squads.Select((s, i) => new
                                                                                             {
                                                                                                 Index = i,
                                                                                                 Text = string.Format(ListItemFormat, s.Name, s.Members.Count(m => m != null)),
                                                                                                 CanAttack = s.Formation.CarryOutAttackOrders
                                                                                             })
                                                                           .Where(s => s.CanAttack)
                                                                           .OrderBy(s => s.Text);

            var newAttackBtn = new Button(panel.Manager);
            newAttackBtn.Init();
            newAttackBtn.Margins = new Margins(4, 0, 4, 0);
            newAttackBtn.Left = moveToBtn.Left + moveToBtn.Width + moveToBtn.Margins.Right + newAttackBtn.Margins.Left;
            newAttackBtn.Top = moveToBtn.Top;
            newAttackBtn.Text = "Attack";
            newAttackBtn.Width = 125;            

            LoweredPanel loweredPanel = new LoweredPanel(panel.Manager);
            loweredPanel.Init();
            loweredPanel.Left = newAttackBtn.Left + newAttackBtn.Width + newAttackBtn.Margins.Right + loweredPanel.Margins.Left;
            loweredPanel.Top = moveToBtn.Top;
            loweredPanel.Width = 235;
            loweredPanel.Height = panel.ClientHeight - loweredPanel.Top - loweredPanel.Margins.Bottom;
            loweredPanel.Anchor = Anchors.Vertical | Anchors.Horizontal;
            loweredPanel.AutoScroll = true;
            loweredPanel.Passive = true;
            loweredPanel.CanFocus = false;

            CheckBoxTree tree = new CheckBoxTree(panel.Manager);
            tree.Init();
            tree.Left = tree.Margins.Left;
            tree.Top = tree.Margins.Top;
            tree.Expanded = true;
            tree.Width = loweredPanel.Width;
            tree.Anchor = Anchors.Top | Anchors.Horizontal;
            tree.Text = Military.AllSquadsDisplay;

            foreach (var squad in squadNames)
                tree.AddChild(CreateCheckbox(panel.Manager, squad.Text, squad.Index));

            tree.EvaluateState();
            panel.Add(loweredPanel);
            loweredPanel.Add(tree);

            newAttackBtn.Click += (object sender, Game.GUI.Controls.EventArgs e) =>
            {
                GnomanEmpire.Instance.Fortress.Military.AddAttackTarget(target);    // For save compatibility
                var checkBoxes = tree.Controls.Where(c => c is ClipBox).Single().Controls.Where(c => c is CheckBox && ((CheckBox)c).Checked && c.Tag != null);
                military.AddTarget(checkBoxes.Select(c => (int)c.Tag), target);
            };

            panel.Add(newAttackBtn);
        }

        /// <summary>
        /// Progress stream position for PatrolRoute class
        /// </summary>
        private static void ReadPatrolRoute(BinaryReader reader)
        {
            ReadDesignation(reader);
            ReadMilitaryStation(reader);

            int num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
            {
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
            }
            reader.ReadBoolean();
            for (int j = 0; j < 5; j++)
                new PatrolRouteStatus(reader);
        }

        /// <summary>
        /// Progress stream position for TrainingStation class
        /// </summary>
        private static void ReadTrainingStation(BinaryReader reader)
        {
            ReadDesignation(reader);
            ReadMilitaryStation(reader);

            reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();

            if (GnomanEmpire.Instance.LoadingSaveVersion >= 8)
                reader.ReadBoolean();
        }

        /// <summary>
        /// Progress stream position for Designation class
        /// </summary>
        private static void ReadDesignation(BinaryReader reader)
        {
            reader.ReadString();
            reader.ReadByte(); reader.ReadByte(); reader.ReadByte(); reader.ReadByte();
            reader.ReadByte(); reader.ReadByte(); reader.ReadByte(); reader.ReadByte();
            int num = reader.ReadInt32();
            for (int i = 0; i < num; i++)
                reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadBoolean();
        }

        /// <summary>
        /// Progress stream position for MilitaryStation class
        /// </summary>
        private static void ReadMilitaryStation(BinaryReader reader)
        {
            if (GnomanEmpire.Instance.LoadingSaveVersion >= 4)
				reader.ReadInt32();
        }

        /// <summary>
        /// Create a checkbox for a particular squad
        /// </summary>
        private static CheckBox CreateCheckbox(Manager manager, string text, int squadIndex)
        {
            CheckBox checkBox = new CheckBox(manager);
            checkBox.Init();
            checkBox.Margins = new Margins(3);
            checkBox.Width = 232;
            checkBox.Height = 20;
            checkBox.Text = text;
            checkBox.Tag = squadIndex;
            checkBox.Checked = true;
            checkBox.Anchor = Anchors.Top | Anchors.Horizontal;

            return checkBox;
        }

        /// <summary>
        /// Remove targets when Game.Squad.Disband is called
        /// </summary>
        public static void Disband(Squad squad)
        {
            military.RemoveSquad(squad);
        }
    }
}
