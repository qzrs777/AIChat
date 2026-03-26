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
        public static MethodInfo _changeAnimTriggerMethod;
        public static MethodInfo _lookInitMethod;
        public static MethodInfo _lookAtMethod;

        private static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 情感标签 → 动画候选列表（触发器名称或整数ID字符串）
        /// 每次调用时随机选一个，让角色表现更丰富
        /// </summary>
        public static readonly Dictionary<string, string[]> EmotionAnimations = new Dictionary<string, string[]>
        {
            ["Happy"] = new[] {
                "id:1001",                          // Story_Joy
                "Story_SubBase001_Joy",
                "Story_SubBase002_Joy",
                "Voice_Motion_Guts_001",
                "Voice_Motion_Guts_002",
                "Voice_Motion_Guts_003",
                "Wild001_Guts_2",
                "Shy_Joy",
            },
            ["Sad"] = new[] {
                "id:1002",                          // Story_Sad
                "Story_SubBase001_Sad",
                "Story_SubBase002_Sad",
                "Story_SubBase003_Sad",
                "Base001_Motion4_Dropshoulders",
                "Shy_Sad",
                "Story_SubBase005_LookDown",
            },
            ["Fun"] = new[] {
                "id:1003",                          // Story_Fun
                "Story_SubBase001_Fun",
                "Story_SubBase002_Fun",
                "Story_SubBase003_Fun",
                "Voice_Motion_Laugh_001",
                "Voice_Motion_Laugh_002",
                "Voice_Motion_Laugh_003",
                "Shy_Fun",
                "BreakBase001_Laugh",
            },
            ["Think"] = new[] {
                "id:252",                           // Thinking
                "Base001_Motion8_Thinking",
                "Base001_Motion9_Start_Thinking2",
                "Base001_Motion10_Start_Thinking3",
                "WorkBase001_Thinking",
                "WorkBase002_Thinking",
                "WorkBase003_BigThinking",
                "WorkBase003_SmallThinking",
                "Voice_Motion_Thinking_001",
                "Voice_Motion_Thinking_002",
            },
            ["Agree"] = new[] {
                "id:1301",                          // Story_Agree
                "Story_SubBase004_Agree",
                "Base001_Motion12_Start_Nod",
                "Voice_Motion_Understand_001",
            },
            ["Confused"] = new[] {
                "id:1302",                          // Story_Frustration
                "Story_SubBase004_Frustration",
                "Base001_Motion1_FrustrationRight",
                "Base001_Motion2_FrustrationLeft",
                "Base001_Motion13_Start_ShakeHead",
                "Voice_Motion_Question_001",
            },
            ["Shy"] = new[] {
                "Base001_Motion5_Shy",
                "Shy_Joy",
                "Shy_Rest",
                "Shy_Surprise",
            },
            ["Angry"] = new[] {
                "Shy_Anger",
                "Story_SubBase005_Denial",
                "Base001_Motion21_Distress",
            },
            ["Surprise"] = new[] {
                "Shy_Surprise",
                "Voice_Motion_JumpUpStart_001",
                "Voice_Motion_JumpUpEnd_001",
                "Base001_Motion6_Jump",
            },
            ["Tired"] = new[] {
                "Wild001_Tired",
                "Wild001_Tired_2",
                "Wild001_Yawn_1",
                "Wild001_Yawn_2",
                "Wild001_DryEye",
            },
            ["Excited"] = new[] {
                "Wild001_Banzai",
                "Wild001_Guts",
                "Wild001_Guts_3",
                "Wild001_Good",
                "Base001_Motion7_Confidence",
                "Voice_Motion_Guts_004",
                "Story_SubBase005_Climax_1",
                "Story_SubBase001_Guts",
            },
            ["Relaxed"] = new[] {
                "id:256",                           // DrinkTea
                "BreakBase004_DrinkTea",
                "BreakBase004_DrinkHot",
                "BreakBase004_CoolingDrinkTea",
                "BreakBase004_Stretch",
                "Wild001_StretchShoulder",
                "Wild001_Tea",
                "Voice_Motion_DrinkHot_001",
                "Voice_Motion_DrinkToCool_001",
            },
            ["Curious"] = new[] {
                "BreakBase002_Interest",
                "BreakBase006_Interest",
                "Voice_Motion_Interest_001",
                "Voice_Motion_Interest_002",
                "Base001_Motion14_Start_LookPenguin",
            },
            ["Greeting"] = new[] {
                "id:5001",                          // WaveHand
                "Base001_Motion15_Start_Introduce",
            },
            ["Working"] = new[] {
                "BreakBase006_Keyboard",
                "BreakBase006_PlayPenLoop",
                "WorkBase002_KeyType",
                "WorkBase002_Loop2_PageFlip",
            },
            ["Sleepy"] = new[] {
                "BreakBase005_Sleep",
                "BreakBase005_GetUpSlowly",
                "Wild001_Yawn_1",
                "Wild001_Yawn_2",
            },
            ["Understand"] = new[] {
                "Voice_Motion_Understand_001",
                "Base001_Motion12_Start_Nod",
                "id:1301",                          // Story_Agree
            },
            ["Nervous"] = new[] {
                "Wild001_Fidget",
                "Base001_Motion21_Distress",
                "BreakBase001_Suspenseful",
                "Wild001_Arm",
            },
            ["Idle"] = new[] {
                "id:250",                           // Idle
            },
        };

        /// <summary>
        /// 根据情感标签随机选一个动画并执行
        /// 返回是否需要特殊流程处理（Greeting/Relaxed 等）
        /// </summary>
        public static string PickRandomAnimation(string emotion)
        {
            if (EmotionAnimations.TryGetValue(emotion, out var candidates) && candidates.Length > 0)
            {
                return candidates[_rng.Next(candidates.Length)];
            }
            return "id:250"; // fallback Idle
        }

        /// <summary>
        /// 执行动画（自动判断触发器 or 整数 ID）
        /// </summary>
        public static void PlayAnimation(string anim)
        {
            if (anim.StartsWith("id:"))
            {
                int id = int.Parse(anim.Substring(3));
                CallNativeChangeAnim(id);
            }
            else
            {
                if (!CallNativeChangeTrigger(anim))
                {
                    Log.Warning($"[动画] 触发器 {anim} 失败，fallback 到 Idle");
                    CallNativeChangeAnim(250);
                }
            }
        }

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
                    _changeAnimTriggerMethod = comp.GetType().GetMethod("ChangeHeroineAnimationForTrigger", BindingFlags.Public | BindingFlags.Instance);
                    _lookInitMethod = comp.GetType().GetMethod("LookInitSlowly", BindingFlags.Public | BindingFlags.Instance);
                    _lookAtMethod = comp.GetType().GetMethod("ChangeLookScaleAnimation", BindingFlags.Public | BindingFlags.Instance);

                    if (_changeAnimSmoothMethod != null) Log.Warning($"✅ 核心连接成功: {comp.gameObject.name}");
                    if (_changeAnimTriggerMethod != null) Log.Warning($"✅ 触发器接口可用");
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

        public static bool CallNativeChangeTrigger(string trigger)
        {
            if (_changeAnimTriggerMethod == null) return false;
            try { _changeAnimTriggerMethod.Invoke(_heroineService, new object[] { trigger }); return true; }
            catch (Exception ex) { Log.Error($"Trigger Error: {ex.Message}"); return false; }
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
