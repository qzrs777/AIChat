using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AIChat.Unity
{
    public static class UIHelper
    {
        public static void ForceShowWindow(GameObject target, Dictionary<GameObject, bool> uiStatusMap)
        {
            target.SetActive(true);
            var p = target.transform.parent;
            while (p != null && p.name != "Canvas")
            {
                if (uiStatusMap != null && !uiStatusMap.ContainsKey(p.gameObject))
                {
                    uiStatusMap.Add(p.gameObject, p.gameObject.activeSelf);
                }
                p.gameObject.SetActive(true);
                p = p.parent;
            }
            foreach (var c in target.GetComponentsInParent<CanvasGroup>()) c.alpha = 1f;
            target.transform.parent.parent.localScale = Vector3.one;
        }

        public static void RestoreUiStatus(Dictionary<GameObject, bool> uiStatusMap, GameObject myTextObj, GameObject originalTextObj)
        {
            if (uiStatusMap != null)
                foreach (var kvp in uiStatusMap)
                {
                    kvp.Key?.SetActive(kvp.Value);
                }
            if (myTextObj != null) UnityEngine.Object.Destroy(myTextObj);
            if (originalTextObj != null) originalTextObj.SetActive(true);
        }

        public static GameObject CreateOverlayText(GameObject parent)
        {
            GameObject go = new GameObject(">>> AI_TEXT <<<");
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            Text txt = go.AddComponent<Text>();
            txt.fontSize = 26;
            txt.alignment = TextAnchor.UpperCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) txt.font = f;
            return go;
        }
    }
}
