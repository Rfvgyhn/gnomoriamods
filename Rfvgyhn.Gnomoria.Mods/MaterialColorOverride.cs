using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Game;
using Game.GUI;
using Game.GUI.Controls;
using GameLibrary;
using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Rfvgyhn.Gnomoria.Mods.Properties;

namespace Rfvgyhn.Gnomoria.Mods
{
    public class MaterialColorOverride : Mod
    {
        static string assemblyName;

        public override string Author
        {
            get
            {
                return "Rfvgyhn";
            }
        }
        
        public override string Name
        {
            get
            {
                return "Material Color Override";
            }
        }

        public override string Description
        {
            get
            {
                return "Allows you to override colors defined in Material.xnb.";
            }
        }

        public override Version Version
        {
            get
            {
                return new Version(1, 0);
            }
        }

        public override IEnumerable<IModification>  Modifications
        {
            get
            {
                return new IModification[]
			    {
                    new MethodHook(typeof(Map).GetConstructor(new Type[] { typeof(TileSet), typeof(BinaryReader) }), Method.Of(new Action<Map, TileSet, BinaryReader>(MapConstructed)), MethodHookType.RunAfter, MethodHookFlags.None),
                    new MethodHook(typeof(Map).GetConstructor(new Type[] { typeof(TileSet) }), Method.Of(new Action<Map, TileSet>(MapConstructed)), MethodHookType.RunAfter, MethodHookFlags.None),
			    };
            }
        }

        public override void Initialize_PreGame()
        {
            base.Initialize_PreGame();

            assemblyName = typeof(MaterialColorOverride).Assembly.GetName().Name;
        }
        
        public static void MapConstructed(Map m, TileSet t, BinaryReader r)
        {
            UpdateColors(m);
        }

        public static void MapConstructed(Map m, TileSet t)
        {
            UpdateColors(m);
        }

        /// <summary>
        /// Read color definitions from file
        /// </summary>
        /// <returns></returns>
        private static IDictionary<string, Vector4> ParseColors()
        {
            const string fileName = "MaterialColorOverride.txt";
            string directory = string.Format("Mods\\Data\\{0}\\", assemblyName);
            var path = Path.Combine(Directory.GetCurrentDirectory(), directory, fileName);            
            Dictionary<string, Vector4> result = new Dictionary<string,Vector4>();
            
            Directory.CreateDirectory(directory);

            if (!File.Exists(path))
                File.WriteAllText(path, Resources.MaterialColorOverride);

            try
            {
                var lines = File.ReadLines(path).Where(l => !l.StartsWith("#"));

                foreach (var str in lines)
                {
                    var groups = Regex.Match(str, @"([a-z]+\s*[a-z]+)\s+(\d.*)", RegexOptions.Singleline).Groups;

                    if (groups.Count != 3)
                        continue;

                    result.Add(groups[1].Value, ParseVector4(groups[2].Value));
                }
            }
            catch { /* swallow */ }                

            return result;
        }

        /// <summary>
        /// Parse RGBA values from file
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        private static Vector4 ParseVector4(string vector)
        {
            var parts = vector.Split(',').Select(i => int.Parse(i.Trim())).ToArray();

            return new Vector4(parts[0], parts[1], parts[2], parts[3]);
        }

        /// <summary>
        /// Modify terrain property colors
        /// </summary>
        /// <param name="m"></param>
        private static void UpdateColors(Map m)
        {
            var colors = ParseColors();

            foreach (var color in colors)
                m.TerrainProperties.Where(p => p.Name == color.Key).Single().Color = color.Value;
        }
    }
}
