using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Makes a level's props (work order 009) from the Crypt models under Resources, loaded by name at run time: no
    /// scene or prefab holds them.
    ///
    /// A prop is an empty object at its place, turned about its up axis, with the model as a child that keeps whatever
    /// rotation and scale its import gave its root, so a turned or scaled root from the import cannot undo the prop's
    /// turn. The object has a collider that matches the footprint and height of <see cref="PropSize.Crypt"/>: a box for
    /// a rectangle, a capsule for a circle, or a box round the circle for one lower than it is wide (a capsule cannot be
    /// lower than its width). No mesh collider. It is on the layer it is given (the walls'). A collider is never lower
    /// than <c>lowestCollider</c>: a level passes a little over the height a NavMesh agent steps up, so the low props
    /// (bone pile, rubble, chest) are not baked into the NavMesh as a step she can walk over.
    ///
    /// Material: the models are painted in flat colour, so each gets a URP Simple Lit material made here with its
    /// texture "_base_color" as its base map, white base colour, no smoothness and no highlights: it takes the scene's
    /// light like the grey boxes do, and shows the painted colour as painted under it.
    /// </summary>
    public static class PropFactory
    {
        public const string ShaderName = "Universal Render Pipeline/Simple Lit";

        static readonly Dictionary<PropModel, GameObject> s_models = new Dictionary<PropModel, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_models.Clear();

        /// <summary>The imported model, loaded once; null when it is not in the project.</summary>
        public static GameObject Model(PropModel model)
        {
            if (s_models.TryGetValue(model, out GameObject go) && go != null) return go;
            go = Resources.Load<GameObject>(PropSize.Of(model).ResourcePath);
            s_models[model] = go;
            return go;
        }

        /// <summary>The model's painted texture: the one its imported material has, else the file beside it by name.</summary>
        public static Texture2D Texture(PropModel model)
        {
            GameObject go = Model(model);
            Renderer r = go != null ? go.GetComponentInChildren<Renderer>() : null;
            Material m = r != null ? r.sharedMaterial : null;
            Texture t = m == null ? null : m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap") : m.mainTexture;
            return t as Texture2D ?? Resources.Load<Texture2D>(PropSize.Of(model).TexturePath);
        }

        /// <summary>A new material for the model (the caller destroys it): Simple Lit with the painted texture.</summary>
        public static Material MakeMaterial(PropModel model)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Renderer r = Model(model) != null ? Model(model).GetComponentInChildren<Renderer>() : null;
                shader = r != null && r.sharedMaterial != null ? r.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            }
            var m = new Material(shader) { name = "Prop_" + PropSize.Of(model).Name };
            Texture2D t = Texture(model);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            m.mainTexture = t;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.color = Color.white;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.DisableKeyword("_SPECULAR_COLOR");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return m;
        }

        /// <summary>
        /// The prop at a world point on the ground, under <paramref name="parent"/>, on <paramref name="layer"/>, with
        /// <paramref name="material"/> on its model (the import's own when null). The model is left out when it cannot
        /// be loaded; the collider is always there.
        /// </summary>
        public static GameObject Make(LevelProp prop, Vector3 ground, Transform parent, int layer, Material material, string name,
                                      float lowestCollider = 0f)
        {
            var root = new GameObject(name) { layer = layer };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, prop.Rotation, 0f));

            GameObject source = Model(prop.Model);
            if (source != null)
            {
                GameObject model = Object.Instantiate(source, root.transform, false);
                model.name = "Model";
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c); // only the footprint's
                if (material != null)
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
            }

            float h = Mathf.Max((float)prop.Height, lowestCollider);
            if (prop.Shape == PropShape.Rectangle)
            {
                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3((float)prop.Width, h, (float)prop.Depth);
                box.center = new Vector3(0f, h * 0.5f, 0f);
            }
            else if (h >= 2f * (float)prop.Radius)
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = (float)prop.Radius;
                capsule.height = h;
                capsule.center = new Vector3(0f, h * 0.5f, 0f);
            }
            else
            {
                var box = root.AddComponent<BoxCollider>();
                float d = 2f * (float)prop.Radius;
                box.size = new Vector3(d, h, d);
                box.center = new Vector3(0f, h * 0.5f, 0f);
            }
            return root;
        }
    }
}
