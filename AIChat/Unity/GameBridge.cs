using AIChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;

namespace AIChat.Unity
{
    public static class GameBridge
    {
        public static MonoBehaviour _heroineService;
        public static Animator _cachedAnimator;

        public static MethodInfo _changeAnimSmoothMethod;
        public static MethodInfo _lookInitMethod;
        public static MethodInfo _lookAtMethod;

        public static void FindHeroineService()
        {
            var allComponents = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var comp in allComponents)
            {
                if (comp.GetType().FullName == "Bulbul.HeroineService")
                {
                    _heroineService = comp;
                    _cachedAnimator = comp.GetComponent<Animator>();

                    _changeAnimSmoothMethod = comp.GetType().GetMethod("ChangeHeroineAnimationForInteger", BindingFlags.Public | BindingFlags.Instance);
                    _lookInitMethod = comp.GetType().GetMethod("LookInitSlowly", BindingFlags.Public | BindingFlags.Instance);
                    _lookAtMethod = comp.GetType().GetMethod("ChangeLookScaleAnimation", BindingFlags.Public | BindingFlags.Instance);

                    if (_changeAnimSmoothMethod != null) Log.Warning($"✅ 核心连接成功: {comp.gameObject.name}");
                    return;
                }
            }
        }
        // --- 辅助方法 ---
        public static void CallNativeChangeAnim(int id)
        {
            try { _changeAnimSmoothMethod.Invoke(_heroineService, new object[] { id }); }
            catch (Exception ex) { Log.Error($"Anim Error: {ex.Message}"); }
        }

        public static void ControlLookAt(float scale, float speed)
        {
            try { _lookAtMethod.Invoke(_heroineService, new object[] { scale, speed, 0 }); }
            catch { }
        }

        public static void RestoreLookAt()
        {
            if (_lookInitMethod != null) try {_lookInitMethod.Invoke(_heroineService, null); } catch { }
        }

    }
}
