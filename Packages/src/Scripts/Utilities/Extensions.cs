using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Coffee.UIParticleExtensions
{
    internal static class Color32Extensions
    {
        private static byte[] s_LinearToGammaLut;

        public static byte LinearToGamma(this byte self)
        {
            if (s_LinearToGammaLut == null)
            {
                s_LinearToGammaLut = new byte[256];
                for (var i = 0; i < 256; i++)
                {
                    s_LinearToGammaLut[i] = (byte)(Mathf.LinearToGammaSpace(i / 255f) * 255f);
                }
            }

            return s_LinearToGammaLut[self];
        }
    }

    internal static class Vector3Extensions
    {
        public static Vector3 Inverse(this Vector3 self)
        {
            self.x = Mathf.Approximately(self.x, 0) ? 1 : 1 / self.x;
            self.y = Mathf.Approximately(self.y, 0) ? 1 : 1 / self.y;
            self.z = Mathf.Approximately(self.z, 0) ? 1 : 1 / self.z;
            return self;
        }

        public static Vector3 GetScaled(this Vector3 self, Vector3 other1)
        {
            self.Scale(other1);
            return self;
        }

        public static Vector3 GetScaled(this Vector3 self, Vector3 other1, Vector3 other2)
        {
            self.Scale(other1);
            self.Scale(other2);
            return self;
        }

        public static Vector3 GetScaled(this Vector3 self, Vector3 other1, Vector3 other2, Vector3 other3)
        {
            self.Scale(other1);
            self.Scale(other2);
            self.Scale(other3);
            return self;
        }

        public static bool IsVisible(this Vector3 self)
        {
            return 0 < Mathf.Abs(self.x * self.y * self.z);
        }
    }

    internal static class SpriteExtensions
    {
#if UNITY_EDITOR
        private static readonly Type s_SpriteEditorExtensionType =
            Type.GetType("UnityEditor.Experimental.U2D.SpriteEditorExtension, UnityEditor")
            ?? Type.GetType("UnityEditor.U2D.SpriteEditorExtension, UnityEditor");

        private static readonly MethodInfo s_GetActiveAtlasTextureMethodInfo = s_SpriteEditorExtensionType
            .GetMethod("GetActiveAtlasTexture", BindingFlags.Static | BindingFlags.NonPublic);

        public static Texture2D GetActualTexture(this Sprite self)
        {
            if (!self) return null;

            if (Application.isPlaying) return self.texture;
            var ret = s_GetActiveAtlasTextureMethodInfo.Invoke(null, new object[] { self }) as Texture2D;
            return ret
                ? ret
                : self.texture;
        }
#else
        public static Texture2D GetActualTexture(this Sprite self)
        {
            return self ? self.texture : null;
        }
#endif
    }

    internal static class ParticleSystemExtensions
    {
        private static ParticleSystem.Particle[] s_TmpParticles = new ParticleSystem.Particle[2048];

        public static ParticleSystem.Particle[] GetParticleArray(int size)
        {
            if (s_TmpParticles.Length < size)
            {
                while (s_TmpParticles.Length < size)
                {
                    size = Mathf.NextPowerOfTwo(size);
                }

                s_TmpParticles = new ParticleSystem.Particle[size];
            }

            return s_TmpParticles;
        }

        public static bool CanBakeMesh(this ParticleSystemRenderer self)
        {
            // #69: Editor crashes when mesh is set to null when `ParticleSystem.RenderMode = Mesh`
            if (self.renderMode == ParticleSystemRenderMode.Mesh && self.mesh == null) return false;

            // #61: When `ParticleSystem.RenderMode = None`, an error occurs
            if (self.renderMode == ParticleSystemRenderMode.None) return false;

            return true;
        }

        public static ParticleSystemSimulationSpace GetActualSimulationSpace(this ParticleSystem self)
        {
            var main = self.main;
            var space = main.simulationSpace;
            if (space == ParticleSystemSimulationSpace.Custom && !main.customSimulationSpace)
            {
                space = ParticleSystemSimulationSpace.Local;
            }

            return space;
        }

        public static bool IsLocalSpace(this ParticleSystem self)
        {
            return GetActualSimulationSpace(self) == ParticleSystemSimulationSpace.Local;
        }

        public static bool IsWorldSpace(this ParticleSystem self)
        {
            return GetActualSimulationSpace(self) == ParticleSystemSimulationSpace.World;
        }

        public static void SortForRendering(this List<ParticleSystem> self, Transform transform, bool sortByMaterial)
        {
            self.Sort((a, b) =>
            {
                var aRenderer = a.GetComponent<ParticleSystemRenderer>();
                var bRenderer = b.GetComponent<ParticleSystemRenderer>();

                // Render queue: ascending
                var aMat = aRenderer.sharedMaterial ? aRenderer.sharedMaterial : aRenderer.trailMaterial;
                var bMat = bRenderer.sharedMaterial ? bRenderer.sharedMaterial : bRenderer.trailMaterial;
                if (!aMat && !bMat) return 0;
                if (!aMat) return -1;
                if (!bMat) return 1;

                if (sortByMaterial)
                {
                    return aMat.GetInstanceID() - bMat.GetInstanceID();
                }

                if (aMat.renderQueue != bMat.renderQueue)
                {
                    return aMat.renderQueue - bMat.renderQueue;
                }

                // Sorting layer: ascending
                if (aRenderer.sortingLayerID != bRenderer.sortingLayerID)
                {
                    return SortingLayer.GetLayerValueFromID(aRenderer.sortingLayerID) -
                           SortingLayer.GetLayerValueFromID(bRenderer.sortingLayerID);
                }

                // Sorting order: ascending
                if (aRenderer.sortingOrder != bRenderer.sortingOrder)
                {
                    return aRenderer.sortingOrder - bRenderer.sortingOrder;
                }

                // Z position & sortingFudge: descending
                var aTransform = a.transform;
                var bTransform = b.transform;
                var aPos = transform.InverseTransformPoint(aTransform.position).z + aRenderer.sortingFudge;
                var bPos = transform.InverseTransformPoint(bTransform.position).z + bRenderer.sortingFudge;
                if (!Mathf.Approximately(aPos, bPos))
                {
                    return (int)Mathf.Sign(bPos - aPos);
                }

                return (int)Mathf.Sign(GetIndex(self, a) - GetIndex(self, b));
            });
        }

        private static int GetIndex(IList<ParticleSystem> list, Object ps)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].GetInstanceID() == ps.GetInstanceID())
                {
                    return i;
                }
            }

            return 0;
        }

        public static Texture2D GetTextureForSprite(this ParticleSystem self)
        {
            if (!self) return null;

            // Get sprite's texture.
            var tsaModule = self.textureSheetAnimation;
            if (!tsaModule.enabled || tsaModule.mode != ParticleSystemAnimationMode.Sprites) return null;

            for (var i = 0; i < tsaModule.spriteCount; i++)
            {
                var sprite = tsaModule.GetSprite(i);
                if (!sprite) continue;

                return sprite.GetActualTexture();
            }

            return null;
        }

        public static void Exec(this List<ParticleSystem> self, Action<ParticleSystem> action)
        {
            foreach (var p in self)
            {
                if (!p) continue;
                action.Invoke(p);
            }
        }
    }

    internal static class Misc
    {
        public static void Destroy(Object obj)
        {
            if (!obj) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(obj);
            }
            else
#endif
            {
                Object.Destroy(obj);
            }
        }

        public static void DestroyImmediate(Object obj)
        {
            if (!obj) return;
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                Object.DestroyImmediate(obj);
            }
            else
#endif
            {
                Object.Destroy(obj);
            }
        }

#if !UNITY_2021_2_OR_NEWER || UNITY_2020_3_45 || UNITY_2020_3_46 || UNITY_2020_3_47 || UNITY_2020_3_48
        public static T GetComponentInParent<T>(this Component self, bool includeInactive) where T : Component
        {
            if (!self) return null;
            if (!includeInactive) return self.GetComponentInParent<T>();

            var current = self.transform;
            while (current)
            {
                var component = current.GetComponent<T>();
                if (component) return component;
                current = current.parent;
            }

            return null;
        }
#endif
    }

    internal static class ListExtensions
    {
        public static void RemoveAtFast<T>(this List<T> self, int index)
        {
            if (self == null) return;

            var lastIndex = self.Count - 1;
            self[index] = self[lastIndex];
            self.RemoveAt(lastIndex);
        }
    }

    internal static class MeshExtensions
    {
        internal static readonly ObjectPool<Mesh> s_MeshPool = new ObjectPool<Mesh>(
            () =>
            {
                var mesh = new Mesh
                {
                    hideFlags = HideFlags.DontSave | HideFlags.NotEditable
                };
                mesh.MarkDynamic();
                return mesh;
            },
            mesh => mesh,
            mesh =>
            {
                if (mesh)
                {
                    mesh.Clear();
                }
            });

        public static Mesh Rent()
        {
            return s_MeshPool.Rent();
        }

        public static void Return(ref Mesh mesh)
        {
            s_MeshPool.Return(ref mesh);
        }
    }

    // internal static class Vector3Extensions
    // {
    //     public static Vector3 Inverse(this Vector3 self)
    //     {
    //         self.x = Mathf.Approximately(self.x, 0) ? 1 : 1 / self.x;
    //         self.y = Mathf.Approximately(self.y, 0) ? 1 : 1 / self.y;
    //         self.z = Mathf.Approximately(self.z, 0) ? 1 : 1 / self.z;
    //         return self;
    //     }
    //
    //     public static Vector3 GetScaled(this Vector3 self, Vector3 other1)
    //     {
    //         self.Scale(other1);
    //         return self;
    //     }
    //
    //     public static Vector3 GetScaled(this Vector3 self, Vector3 other1, Vector3 other2)
    //     {
    //         self.Scale(other1);
    //         self.Scale(other2);
    //         return self;
    //     }
    //
    //     public static bool IsVisible(this Vector3 self)
    //     {
    //         return 0 < Mathf.Abs(self.x * self.y);
    //     }
    // }

    /// <summary>
    /// Extension methods for Graphic class.
    /// </summary>
    internal static class GraphicExtensions
    {
        private static readonly Vector3[] s_WorldCorners = new Vector3[4];
        private static readonly Bounds s_ScreenBounds = new Bounds(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1, 1, 1));

        /// <summary>
        /// Check if a Graphic component is currently in the screen view.
        /// </summary>
        public static bool IsInScreen(this Graphic self)
        {
            if (!self || !self.canvas) return false;

            if (FrameCache.TryGet(self, nameof(IsInScreen), out bool result))
            {
                return result;
            }

            Profiler.BeginSample("(CCR)[GraphicExtensions] InScreen");
            var cam = self.canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? self.canvas.worldCamera
                : null;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            self.rectTransform.GetWorldCorners(s_WorldCorners);

            for (var i = 0; i < 4; i++)
            {
                if (cam)
                {
                    s_WorldCorners[i] = cam.WorldToViewportPoint(s_WorldCorners[i]);
                }
                else
                {
                    s_WorldCorners[i] = RectTransformUtility.WorldToScreenPoint(null, s_WorldCorners[i]);
                    s_WorldCorners[i].x /= Screen.width;
                    s_WorldCorners[i].y /= Screen.height;
                }

                s_WorldCorners[i].z = 0;
                min = Vector3.Min(s_WorldCorners[i], min);
                max = Vector3.Max(s_WorldCorners[i], max);
            }

            var bounds = new Bounds(min, Vector3.zero);
            bounds.Encapsulate(max);
            result = bounds.Intersects(s_ScreenBounds);
            FrameCache.Set(self, nameof(IsInScreen), result);
            Profiler.EndSample();

            return result;
        }

        public static float GetParentGroupAlpha(this Graphic self)
        {
            var alpha = self.canvasRenderer.GetAlpha();
            if (Mathf.Approximately(alpha, 0)) return 1;

            var inheritedAlpha = self.canvasRenderer.GetInheritedAlpha();
            return Mathf.Clamp01(inheritedAlpha / alpha);
        }
    }
}
