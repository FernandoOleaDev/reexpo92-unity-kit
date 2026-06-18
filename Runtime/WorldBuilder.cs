using System;
using System.Linq;
using UnityEngine;

namespace ReExpo92.WorldKit
{
    /// <summary>Opciones para construir el mundo georreferenciado en la escena.</summary>
    public class WorldBuildOptions
    {
        public string GoogleApiKey;        // null/"" => sin maqueta de Google
        public MapData Data;               // POIs + zonas (puede ser null)
        public bool ShowGoogleTiles = true;
        public bool ShowPois = true;
        public bool ShowZones = true;
    }

    /// <summary>
    /// Constructor del rig georreferenciado. La implementación real vive en el
    /// ensamblado opcional ReExpo92.WorldKit.Cesium, que SOLO se compila si está
    /// instalado Cesium for Unity (defineConstraint REEXPO_CESIUM). Así el package
    /// compila aunque Cesium no esté presente; el editor lo localiza por reflexión.
    /// </summary>
    public interface IWorldBuilder
    {
        string Name { get; }
        GameObject Build(WorldBuildOptions options);
    }

    /// <summary>Localiza por reflexión una implementación de IWorldBuilder.</summary>
    public static class WorldBuilderLocator
    {
        public static bool IsAvailable => Find() != null;

        public static IWorldBuilder Find()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                var t = types.FirstOrDefault(x =>
                    typeof(IWorldBuilder).IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface);
                if (t != null)
                {
                    try { return (IWorldBuilder)Activator.CreateInstance(t); }
                    catch { /* siguiente */ }
                }
            }
            return null;
        }
    }
}
