using UnityEngine;
using UnityEngine.Events;               
using UnityEngine.UI;                   

namespace BlocXplode
{
    public sealed class UIBuilder
    {
        public sealed class ButtonParts
        {
            public Button button;       
            public Image background;   
            public Text label;          
            public Image icon;        
        }

        readonly SpriteLibrary sprites; 
        readonly UITheme theme;        
        readonly Font font;             

        public UIBuilder(SpriteLibrary spriteLibrary, UITheme uiTheme)
        {
            sprites = spriteLibrary;
            theme = uiTheme;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");  
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));  
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);        
            rt.anchorMin = anchorMin;           
            rt.anchorMax = anchorMax;           
            rt.offsetMin = Vector2.zero;       
            rt.offsetMax = Vector2.zero;        
            return rt;
        }

        public Image Panel(RectTransform rt, Color color, bool blocksClicks)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprites.Panel;
            img.type = Image.Type.Sliced;      
            img.color = color;                 
            img.raycastTarget = blocksClicks;   
            return img;
        }

        public Image Solid(RectTransform rt, Color color, bool blocksClicks)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprites.Pixel;
            img.color = color;
            img.raycastTarget = blocksClicks;
            return img;
        }

        public Text Label(RectTransform parent, string content, int size, TextAnchor anchor, Color color, bool shadow = true)
        {
            var rt = Rect("Text", parent, Vector2.zero, Vector2.one);   
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;       
            t.alignment = anchor;               
            t.color = color;
            t.text = content;
            t.raycastTarget = false;            
            t.horizontalOverflow = HorizontalWrapMode.Overflow;   
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();   
                sh.effectColor = new Color(0f, 0f, 0f, 0.45f);    
                sh.effectDistance = new Vector2(0f, -4f);        
            }
            return t;
        }

        public ButtonParts Button(RectTransform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            Color color, UnityAction onClick, int fontSize = 44, Sprite icon = null)
        {
            var rt = Rect(label.Length > 0 ? label + "Button" : "IconButton", parent, anchorMin, anchorMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprites.Button;        
            img.type = Image.Type.Sliced;
            img.color = color;                  

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;           
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;            
            colors.normalColor = Color.white;                       
            colors.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);   
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);      
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.05f;       
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };  
            btn.onClick.AddListener(onClick);   

            var parts = new ButtonParts { button = btn, background = img };

            bool hasLabel = label.Length > 0;
            if (icon != null)
            {
                var iconRect = hasLabel
                    ? Rect("Icon", rt, new Vector2(0.07f, 0.22f), new Vector2(0.30f, 0.86f))
                    : Rect("Icon", rt, new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.88f));
                var iconImg = iconRect.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;  
                iconImg.raycastTarget = false;
                parts.icon = iconImg;
            }

            if (hasLabel)
            {
                var labelRect = Rect("Label", rt, icon != null ? new Vector2(0.30f, 0.14f) : new Vector2(0.04f, 0.14f),
                    new Vector2(0.97f, 1f));
                parts.label = Label(labelRect, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            }
            return parts;
        }
    }
}
