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
                "id:1001",   // Story_SubBase001_Joy
                "id:1101",   // Story_SubBase002_Joy
                "id:1004",   // Story_SubBase001_Guts
                "id:7",      // Base001_Motion7_Confidence
                "id:6",      // Base001_Motion6_Jump
            },
            ["Sad"] = new[] {
                "id:1002",   // Story_SubBase001_Sad
                "id:1102",   // Story_SubBase002_Sad
                "id:1201",   // Story_SubBase003_Sad
                "id:4",      // Base001_Motion4_Dropshoulders
                "id:1401",   // Story_SubBase005_LookDown
            },
            ["Fun"] = new[] {
                "id:1003",   // Story_SubBase001_Fun
                "id:1103",   // Story_SubBase002_Fun
                "id:1202",   // Story_SubBase003_Fun
                "id:601",    // BreakBase001_Laugh
            },
            ["Think"] = new[] {
                "id:252",    // WorkBase002_Thinking
                "id:8",      // Base001_Motion8_Thinking
                "id:9",      // Base001_Motion9_Start_Thinking2
                "id:10",     // Base001_Motion10_Start_Thinking3
                "id:202",    // WorkBase001_Thinking
                "id:302",    // WorkBase003_BigThinking
                "id:301",    // WorkBase003_SmallThinking
            },
            ["Agree"] = new[] {
                "id:1301",   // Story_SubBase004_Agree
                "id:12",     // Base001_Motion12_Start_Nod
                "id:1402",   // Story_SubBase005_Nod
            },
            ["Confused"] = new[] {
                "id:1302",   // Story_SubBase004_Frustration
                "id:1",      // Base001_Motion1_FrustrationRight
                "id:2",      // Base001_Motion2_FrustrationLeft
                "id:13",     // Base001_Motion13_Start_ShakeHead
            },
            ["Shy"] = new[] {
                "id:5",      // Base001_Motion5_Shy
                "id:3",      // Base001_Motion3_PressHands
                "id:20",     // Base001_Motion20_Eieio
            },
            ["Angry"] = new[] {
                "id:1403",   // Story_SubBase005_Denial
                "id:21",     // Base001_Motion21_Distress
                "id:1302",   // Story_SubBase004_Frustration
                "id:13",     // Base001_Motion13_Start_ShakeHead
            },
            ["Surprise"] = new[] {
                "id:6",      // Base001_Motion6_Jump
                "id:803",    // BreakBase005_JumpUp
                "id:1003",   // Story_SubBase001_Fun
            },
            ["Tired"] = new[] {
                "id:64",     // Wild001_Motion15_Tired
                "id:67",     // Wild001_Motion18_Tired_2
                "id:72",     // Wild001_Motion23_Yawn_1
                "id:73",     // Wild001_Motion24_Yawn_2
                "id:70",     // Wild001_Motion21_DryEye
            },
            ["Excited"] = new[] {
                "id:55",     // Wild001_Motion6_Banzai
                "id:53",     // Wild001_Motion4_Guts
                "id:71",     // Wild001_Motion22_Guts_3
                "id:61",     // Wild001_Motion12_Good
                "id:7",      // Base001_Motion7_Confidence
                "id:65",     // Wild001_Motion16_CompleteTask
                "id:1406",   // Story_SubBase005_Climax_1
            },
            ["Relaxed"] = new[] {
                "id:256",    // WorkBase002_DrinkTea
                "id:751",    // BreakBase004_DrinkTea
                "id:752",    // BreakBase004_Stretch
                "id:753",    // BreakBase004_DrinkHot
                "id:755",    // BreakBase004_CoolingDrinkTea
                "id:50",     // Wild001_Motion1_StretchFllBody
                "id:51",     // Wild001_Motion2_StretchShoulder
                "id:52",     // Wild001_Motion3_Tea
            },
            ["Curious"] = new[] {
                "id:653",    // BreakBase002_Interest
                "id:851",    // BreakBase006_Interest
                "id:14",     // Base001_Motion14_Start_LookPenguin
            },
            ["Greeting"] = new[] {
                "id:5001",   // WantTalk_Base_001_WaveHandShortTime
                "id:15",     // Base001_Motion15_Start_Introduce
                "id:5002",   // WantTalk_Base_001_LeaningForward
            },
            ["Working"] = new[] {
                "id:852",    // BreakBase006_Keyboard
                "id:853",    // BreakBase006_PlayPenLoop
                "id:251",    // WorkBase002_KeyType
                "id:255",    // WorkBase002_Loop2_PageFlip
                "id:253",    // WorkBase002_PageFlip
            },
            ["Sleepy"] = new[] {
                "id:801",    // BreakBase005_Sleep
                "id:802",    // BreakBase005_GetUpSlowly
                "id:72",     // Wild001_Motion23_Yawn_1
                "id:73",     // Wild001_Motion24_Yawn_2
            },
            ["Understand"] = new[] {
                "id:1301",   // Story_SubBase004_Agree
                "id:12",     // Base001_Motion12_Start_Nod
                "id:1402",   // Story_SubBase005_Nod
            },
            ["Nervous"] = new[] {
                "id:62",     // Wild001_Motion13_Fidget
                "id:21",     // Base001_Motion21_Distress
                "id:602",    // BreakBase001_Suspenseful
                "id:63",     // Wild001_Motion14_Arm
                "id:3",      // Base001_Motion3_PressHands
            },
            ["Idle"] = new[] {
                "id:250",    // WorkBase002 默认
                "id:0",      // Base001 默认
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
            try
            {
                // 获取方法参数类型（AnimationType 枚举）
                var paramType = _changeAnimTriggerMethod.GetParameters()[0].ParameterType;
                if (paramType.IsEnum)
                {
                    // 将字符串转换为枚举值
                    var enumValue = Enum.Parse(paramType, trigger);
                    _changeAnimTriggerMethod.Invoke(_heroineService, new object[] { enumValue });
                }
                else
                {
                    _changeAnimTriggerMethod.Invoke(_heroineService, new object[] { trigger });
                }
                return true;
            }
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
