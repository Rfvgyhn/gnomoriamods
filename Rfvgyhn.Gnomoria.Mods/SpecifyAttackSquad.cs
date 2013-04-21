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
        Version version = AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Version;

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
                return version;
            }
        }

        public override IMethodModification[] Hooks
        {
            get
            {
                return new IMethodModification[]
			    {
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

        public static void LoadTargets(Game.Military m, BinaryReader b)
        {
            var targets = typeof(Game.Military).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(f => f.FieldType == typeof(List<Character>));

            foreach (var target in targets)
            {
                var value = (List<Character>)target.GetValue(m);

                if (value.Any())
                    value.ForEach(t => military.AddTarget(t));
            }
        }

        public static Character FindAttackTarget(Game.Military m, Character c)
        {
            return military.FindAttackTarget(c);
        }

        public static void RemoveAttackTarget(Game.Military m, Character c)
        {
            military.RemoveAttackTarget(c);
        }

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

        public static void Disband(Squad squad)
        {
            military.RemoveSquad(squad);
        }
    }
}
