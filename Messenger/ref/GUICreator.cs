﻿//#define DEBUG

using Facepunch.Extend;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GUICreator", "OHM", "1.2.5")]
    [Description("GUICreator")]
    internal class GUICreator : RustPlugin
    {
        #region global

        [PluginReference]
        private Plugin ImageLibrary;

        private static GUICreator PluginInstance = null;

        public GUICreator()
        {
            PluginInstance = this;
        }

        #endregion global

        #region oxide hooks

        private void Init()
        {
            permission.RegisterPermission("gui.demo", this);

            lang.RegisterMessages(messages, this);
            cmd.AddConsoleCommand("gui.close", this, nameof(closeUi));
            cmd.AddConsoleCommand("gui.input", this, nameof(OnGuiInput));
            cmd.AddConsoleCommand("gui.list", this, nameof(listContainers));
        }

        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                Puts("ImageLibrary is not loaded! get it here https://umod.org/plugins/image-library");
                return;
            }
            registerImage(this, "flower", "https://i.imgur.com/uAhjMNd.jpg");
            registerImage(this, "gameTipIcon", config.gameTipIcon);
            registerImage(this, "bgTex", "https://i.imgur.com/OAa71Rt.png");
            registerImage(this, "warning_alpha", "https://i.imgur.com/u0bNKXx.png");
            registerImage(this, "white_cross", "https://i.imgur.com/fbwkYDj.png");
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                GuiTracker.getGuiTracker(player).destroyAllGui();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            GuiTracker.getGuiTracker(player).destroyAllGui();
        }

        private object OnPlayerWound(BasePlayer player)
        {
            GuiTracker.getGuiTracker(player).destroyAllGui();
            return null;
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            GuiTracker.getGuiTracker(player).destroyAllGui();
            return null;
        }

        #endregion oxide hooks

        #region classes

        public class Rectangle : CuiRectTransformComponent
        {
            public double anchorMinX;
            public double anchorMinY;
            public double anchorMaxX;
            public double anchorMaxY;

            public double X;
            public double Y;
            public double W;
            public double H;

            bool topLeftOrigin;
            //public string anchorMin => $"{anchorMinX} {anchorMinY}";
            //public string anchorMax => $"{anchorMaxX} {anchorMaxY}";

            public Rectangle()
            {
                AnchorMin = "0 0";
                AnchorMax = "1 1";
            }

            public Rectangle(double X, double Y, double W, double H, double resX = 1, double resY = 1, bool topLeftOrigin = false)
            {
                this.X = X;
                this.Y = Y;
                this.W = W;
                this.H = H;
                this.topLeftOrigin = topLeftOrigin;

                double newY = topLeftOrigin ? resY - Y - H : Y;

                anchorMinX = X / resX;
                anchorMinY = newY / resY;
                anchorMaxX = (X + W) / resX;
                anchorMaxY = (newY + H) / resY;

                AnchorMin = $"{anchorMinX} {anchorMinY}";
                AnchorMax = $"{anchorMaxX} {anchorMaxY}";
            }
        }

        public class GuiColor
        {
            private Color color;

            public GuiColor()
            {
                color = new Color(1, 1, 1, 1);
            }

            public GuiColor(float R, float G, float B, float alpha)
            {
                if (R > 1) R /= 255;
                if (G > 1) G /= 255;
                if (B > 1) B /= 255;
                color = new Color(R, G, B, alpha);
            }

            public GuiColor(string hex)
            {
                ColorUtility.TryParseHtmlString(hex, out color);
            }

            public void setAlpha(float alpha)
            {
                setAlpha(this, alpha);
            }

            public static GuiColor setAlpha(GuiColor color, float alpha)
            {
                color.color.a = alpha;
                return color;
            }

            public string getColorString()
            {
                return $"{color.r} {color.g} {color.b} {color.a}";
            }
        }

        public class GuiText : CuiTextComponent
        {
            public GuiText()
            {
            }

            public GuiText(string text, int fontSize = 14, GuiColor color = null, TextAnchor align = TextAnchor.MiddleCenter, float FadeIn = 0)
            {
                this.Text = text;
                this.FontSize = fontSize;
                this.Align = align;
                this.Color = color?.getColorString() ?? new GuiColor(0, 0, 0, 1).getColorString();
                this.FadeIn = FadeIn;
            }
        }

        public class CuiInputField
        {
            //public CuiImageComponent Image { get; set; } = new CuiImageComponent();
            public CuiInputFieldComponent InputField { get; set; } = new CuiInputFieldComponent();

            public CuiRectTransformComponent RectTransform { get; set; } = new CuiRectTransformComponent();
            public bool CursorEnabled { get; set; }
            public float FadeOut { get; set; }
        }

        public class GuiContainer : CuiElementContainer
        {
            public Plugin plugin;
            public string name;
            public string parent;
            public List<Timer> timers = new List<Timer>();
            public Action<BasePlayer> closeCallback;
            private Dictionary<string, Action<BasePlayer, string[]>> callbacks = new Dictionary<string, Action<BasePlayer, string[]>>();

            public GuiContainer(Plugin plugin, string name, string parent = null, Action<BasePlayer> closeCallback = null)
            {
                this.plugin = plugin;
                this.name = safeName(name);
                this.parent = safeName(parent);
                this.closeCallback = closeCallback;
            }

            public enum Layer { overall, overlay, menu, hud, under };

            //Rust UI elements (inventory, Health, etc.) are between the hud and the menu layer
            public static List<string> layers = new List<string> { "Overall", "Overlay", "Hud.Menu", "Hud", "Under" };

            public void display(BasePlayer player)
            {
                if (this.Count == 0) return;
                GuiTracker.getGuiTracker(player).addGuiToTracker(plugin, this);
                CuiHelper.AddUi(player, this);
            }

            public void destroy(BasePlayer player)
            {
                GuiTracker.getGuiTracker(player).destroyGui(plugin, this);
            }

            public void registerCallback(string name, Action<BasePlayer, string[]> callback)
            {
                if (callbacks.ContainsKey(name)) callbacks.Remove(name);
                callbacks.Add(name, callback);
            }

            public bool runCallback(string name, BasePlayer player, string[] input)
            {
                if (!callbacks.ContainsKey(name)) return false;
                try
                {
                    callbacks[name](player, input);
                    return true;
                }
                catch (Exception E)
                {
                    PluginInstance.Puts($"Failed to run callback: {name}, {E.Message}");
                    return false;
                }
            }

            private void purgeDuplicates(string name)
            {
                foreach (CuiElement element in this)
                {
                    if (element.Name == name)
                    {
                        this.Remove(element);
                        return;
                    }
                }
            }

            public string Add(CuiInputField InputField, string Parent = "Hud", string Name = "")
            {
                if (string.IsNullOrEmpty(Name)) Name = CuiHelper.GetGuid();

                CuiElement element = new CuiElement()
                {
                    Name = Name,
                    Parent = Parent,
                    FadeOut = InputField.FadeOut,
                    Components = {
                    InputField.InputField,
                    InputField.RectTransform
                }
                };
                //if (InputField.Image != null) element.Components.Add(InputField.Image);
                if (InputField.CursorEnabled) element.Components.Add(new CuiNeedsCursorComponent());

                Add(element);
                return Name;
            }

            public void addPanel(string name, CuiRectTransformComponent rectangle, Layer layer, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, GuiText text = null, string imgName = null, bool blur = false)
            {
                addPanel(name, rectangle, layers[(int)layer], panelColor, FadeIn, FadeOut, text, imgName, blur);
            }

            public void addPanel(string name, CuiRectTransformComponent rectangle, string parent = "Hud", GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, GuiText text = null, string imgName = null, bool blur = false)
            {
                if (string.IsNullOrEmpty(name)) name = "panel";
                else name = PluginInstance.encodeName(this, name);
                purgeDuplicates(name);

                if (string.IsNullOrEmpty(imgName))
                {
                    addPlainPanel(PluginInstance.decodeName(this, name), rectangle, parent, panelColor, FadeIn, FadeOut, blur);
                }
                else
                {
                    this.addImage(PluginInstance.decodeName(this, name), rectangle, imgName, parent, panelColor, FadeIn, FadeOut);
                }
                if (text != null) this.addText(PluginInstance.decodeName(this, name) + "_txt", new Rectangle(), text, FadeIn, FadeOut, PluginInstance.decodeName(this, name));
            }

            public void addPlainPanel(string name, CuiRectTransformComponent rectangle, Layer layer, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, bool blur = false)
            {
                addPlainPanel(name, rectangle, layers[(int)layer], panelColor, FadeIn, FadeOut, blur);
            }

            public void addPlainPanel(string name, CuiRectTransformComponent rectangle, string parent = "Hud", GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, bool blur = false)
            {
                if (string.IsNullOrEmpty(name)) name = "plainPanel";
                else name = PluginInstance.encodeName(this, name);
                purgeDuplicates(name);

                this.Add(new CuiElement
                {
                    Parent = PluginInstance.encodeName(this, parent),
                    Name = name,
                    Components =
                {
                    new CuiImageComponent { Color = (panelColor != null)?panelColor.getColorString():"0 0 0 0", FadeIn = FadeIn, Material = blur?"assets/content/ui/uibackgroundblur-ingamemenu.mat":"Assets/Icons/IconMaterial.mat"},
                    rectangle
                },
                    FadeOut = FadeOut
                });
            }

            public void addImage(string name, CuiRectTransformComponent rectangle, string imgName, Layer layer, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0)
            {
                addImage(name, rectangle, imgName, layers[(int)layer], panelColor, FadeIn, FadeOut);
            }

            public void addImage(string name, CuiRectTransformComponent rectangle, string imgName, string parent = "Hud", GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0)
            {
                if (string.IsNullOrEmpty(name)) name = "image";
                else name = PluginInstance.encodeName(this, name); ;
                purgeDuplicates(name);

                this.Add(new CuiElement
                {
                    Parent = PluginInstance.encodeName(this, parent),
                    Name = name,
                    Components =
                {
                    new CuiRawImageComponent { Color = (panelColor != null)?panelColor.getColorString():"1 1 1 1", FadeIn = FadeIn, Png = PluginInstance.getImageData(plugin, imgName)},
                    rectangle
                },
                    FadeOut = FadeOut
                });
            }

            public void addRawImage(string name, CuiRectTransformComponent rectangle, string imgData, Layer layer, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0)
            {
                addRawImage(name, rectangle, imgData, layers[(int)layer], panelColor, FadeIn, FadeOut);
            }

            public void addRawImage(string name, CuiRectTransformComponent rectangle, string imgData, string parent = "Hud", GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0)
            {
                if (string.IsNullOrEmpty(name)) name = "image";
                else name = PluginInstance.encodeName(this, name);
                purgeDuplicates(name);

                this.Add(new CuiElement
                {
                    Parent = PluginInstance.encodeName(this, parent),
                    Name = name,
                    Components =
                {
                    new CuiRawImageComponent { Color = (panelColor != null)?panelColor.getColorString():"1 1 1 1", FadeIn = FadeIn, Png = imgData},
                    rectangle
                },
                    FadeOut = FadeOut
                });
            }

            public void addText(string name, CuiRectTransformComponent rectangle, Layer layer, GuiText text = null, float FadeIn = 0, float FadeOut = 0)
            {
                addText(name, rectangle, text, FadeIn, FadeOut, layers[(int)layer]);
            }

            public void addText(string name, CuiRectTransformComponent rectangle, GuiText text = null, float FadeIn = 0, float FadeOut = 0, string parent = "Hud")
            {
                if (string.IsNullOrEmpty(name)) name = "text";
                else name = PluginInstance.encodeName(this, name);
                purgeDuplicates(name);

                text.FadeIn = FadeIn;

                this.Add(new CuiElement
                {
                    Parent = PluginInstance.encodeName(this, parent),
                    Name = name,
                    Components =
                {
                    text,
                    rectangle
                },
                    FadeOut = FadeOut
                });
            }

            public void addButton(string name, CuiRectTransformComponent rectangle, Layer layer, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, GuiText text = null, Action<BasePlayer, string[]> callback = null, string close = null, bool CursorEnabled = true, string imgName = null, bool blur = false)
            {
                addButton(name, rectangle, panelColor, FadeIn, FadeOut, text, callback, close, CursorEnabled, imgName, layers[(int)layer], blur);
            }

            public void addButton(string name, CuiRectTransformComponent rectangle, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, GuiText text = null, Action<BasePlayer, string[]> callback = null, string close = null, bool CursorEnabled = true, string imgName = null, string parent = "Hud", bool blur = false)
            {
                if (string.IsNullOrEmpty(name)) name = "button";
                else name = PluginInstance.encodeName(this, name);
                purgeDuplicates(name);

                if (imgName != null)
                {
                    this.addImage(PluginInstance.decodeName(this, name), rectangle, imgName, parent, null, FadeIn, FadeOut);
                    this.addPlainButton(PluginInstance.decodeName(this, name) + "_btn", new Rectangle(), panelColor, FadeIn, FadeOut, text, callback, close, CursorEnabled, PluginInstance.decodeName(this, name));
                }
                else
                {
                    this.addPlainButton(PluginInstance.decodeName(this, name), rectangle, panelColor, FadeIn, FadeOut, text, callback, close, CursorEnabled, parent, blur);
                }
            }

            public void addPlainButton(string name, CuiRectTransformComponent rectangle, Layer layer, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, GuiText text = null, Action<BasePlayer, string[]> callback = null, string close = null, bool CursorEnabled = true, bool blur = false)
            {
                addPlainButton(name, rectangle, panelColor, FadeIn, FadeOut, text, callback, close, CursorEnabled, layers[(int)layer], blur);
            }

            public void addPlainButton(string name, CuiRectTransformComponent rectangle, GuiColor panelColor = null, float FadeIn = 0, float FadeOut = 0, GuiText text = null, Action<BasePlayer, string[]> callback = null, string close = null, bool CursorEnabled = true, string parent = "Hud", bool blur = false)
            {
                if (string.IsNullOrEmpty(name)) name = "plainButton";
                else name = PluginInstance.encodeName(this, name);
                purgeDuplicates(name);

                StringBuilder closeString = new StringBuilder("");
                if (close != null)
                {
                    closeString.Append(" --close ");
                    closeString.Append(close);
                }

                this.Add(new CuiElement
                {
                    Name = name,
                    Parent = PluginInstance.encodeName(this, parent),
                    Components =
                    {
                        new CuiButtonComponent {Command = $"gui.input {plugin.Name} {this.name} {name}{closeString.ToString()}", FadeIn = FadeIn, Color = (panelColor != null) ? panelColor.getColorString() : "0 0 0 0", Material = blur?"assets/content/ui/uibackgroundblur-ingamemenu.mat":"Assets/Icons/IconMaterial.mat"},
                        rectangle
                    },
                    FadeOut = FadeOut
                });

                if (text != null) this.addText(PluginInstance.decodeName(this, name) + "_txt", new CuiRectTransformComponent(), text, FadeIn, FadeOut, PluginInstance.decodeName(this, name));

                if (CursorEnabled)
                {
                    this.Add(new CuiElement()
                    {
                        Name = PluginInstance.decodeName(this, name) + "_cursor",
                        Parent = name,
                        Components =
                    {
                        new CuiNeedsCursorComponent()
                    }
                    });
                }

                if (callback != null) this.registerCallback(name, callback);
            }

            public void addInput(string name, CuiRectTransformComponent rectangle, Action<BasePlayer, string[]> callback, Layer layer, string close = null, GuiColor panelColor = null, int charLimit = 100, GuiText text = null, float FadeIn = 0, float FadeOut = 0, bool isPassword = false, bool CursorEnabled = true, string imgName = null)
            {
                addInput(name, rectangle, callback, layers[(int)layer], close, panelColor, charLimit, text, FadeIn, FadeOut, isPassword, CursorEnabled, imgName);
            }

            public void addInput(string name, CuiRectTransformComponent rectangle, Action<BasePlayer, string[]> callback, string parent = "Hud", string close = null, GuiColor panelColor = null, int charLimit = 100, GuiText text = null, float FadeIn = 0, float FadeOut = 0, bool isPassword = false, bool CursorEnabled = true, string imgName = null)
            {
                if (string.IsNullOrEmpty(name)) name = "input";
                else name = PluginInstance.encodeName(this, name);
                purgeDuplicates(name);

                StringBuilder closeString = new StringBuilder("");
                if (close != null)
                {
                    closeString.Append(" --close ");
                    closeString.Append(close);
                }

                if (imgName != null || panelColor != null)
                {
                    this.addPanel(PluginInstance.decodeName(this, name), rectangle, parent, panelColor, FadeIn, FadeOut, null, imgName);

                    this.Add(new CuiInputField()
                    {
                        InputField = { Align = text.Align, FontSize = text.FontSize, Color = text.Color, Command = $"gui.input {plugin.Name} {this.name} {name}{closeString.ToString()} --input", CharsLimit = charLimit, IsPassword = isPassword },
                        RectTransform = new Rectangle(),
                        CursorEnabled = CursorEnabled,
                        FadeOut = FadeOut
                    }, name, name + "_ipt");
                }
                else
                {
                    this.Add(new CuiInputField()
                    {
                        InputField = { Align = text.Align, FontSize = text.FontSize, Color = text.Color, Command = $"gui.input text {plugin.Name} {this.name} {name}{closeString.ToString()} --input", CharsLimit = charLimit, IsPassword = isPassword },
                        RectTransform = rectangle,
                        CursorEnabled = CursorEnabled,
                        FadeOut = FadeOut
                    }, PluginInstance.encodeName(this, parent), name);
                }

                if (callback != null) this.registerCallback(name, callback);
            }
        }

        public class GuiTracker : MonoBehaviour
        {
            private BasePlayer player;
            public List<GuiContainer> activeGuiContainers = new List<GuiContainer>();

            public static GuiTracker getGuiTracker(BasePlayer player)
            {
                GuiTracker output = null;

                player.gameObject.TryGetComponent<GuiTracker>(out output);

                if (output == null)
                {
                    output = player.gameObject.AddComponent<GuiTracker>();
                    output.player = player;
                }

                return output;
            }

            public GuiContainer getContainer(Plugin plugin, string name)
            {
                name = safeName(name);
                foreach (GuiContainer container in activeGuiContainers)
                {
                    if (container.plugin != plugin) continue;
                    if (container.name == name) return container;
                }
                return null;
            }

            public void addGuiToTracker(Plugin plugin, GuiContainer container)
            {
#if DEBUG
                player.ChatMessage($"adding {container.name} to tracker");
#endif
                if (getContainer(plugin, container.name) != null) destroyGui(plugin, container.name);
                activeGuiContainers.Add(container);
            }

            public void destroyGui(Plugin plugin, string containerName, string name = null)
            {
                if (name != null) name = safeName(name);
                destroyGui(plugin, getContainer(plugin, safeName(containerName)), name);
            }

            public void destroyGui(Plugin plugin, GuiContainer container, string name = null)
            {
                if (container == null) return;
                if (name == null)
                {
                    List<GuiContainer> garbage = new List<GuiContainer>();
                    destroyGuiContainer(plugin, container, garbage);
                    foreach (GuiContainer cont in garbage)
                    {
                        activeGuiContainers.Remove(cont);
                    }
                }
                else
                {
                    name = safeName(name);
                    List<CuiElement> eGarbage = new List<CuiElement>();
                    destroyGuiElement(plugin, container, name, eGarbage);
                    foreach (CuiElement element in eGarbage)
                    {
                        container.Remove(element);
                    }
                }
            }

            private void destroyGuiContainer(Plugin plugin, GuiContainer container, List<GuiContainer> garbage)
            {
#if DEBUG
                player.ChatMessage($"destroyGuiContainer: start {plugin.Name} {container.name}");
#endif
                if (activeGuiContainers.Contains(container))
                {
                    foreach (GuiContainer cont in activeGuiContainers)
                    {
                        if (cont.plugin != container.plugin) continue;
                        if (cont.parent == container.name) destroyGuiContainer(cont.plugin, cont, garbage);
                    }
                    container.closeCallback?.Invoke(player);
                    List<CuiElement> eGarbage = new List<CuiElement>();
                    foreach (CuiElement element in container)
                    {
                        destroyGuiElement(plugin, container, element.Name, eGarbage);
                    }
                    foreach (CuiElement element in eGarbage)
                    {
                        container.Remove(element);
                    }
                    foreach (Timer timer in container.timers)
                    {
                        timer.Destroy();
                    }
                    garbage.Add(container);
                }
                else PluginInstance.Puts($"destroyGui(container.name: {container.name}): no GUI containers found");
            }

            private void destroyGuiElement(Plugin plugin, GuiContainer container, string name, List<CuiElement> garbage)
            {
                name = safeName(name);
#if DEBUG
                player.ChatMessage($"destroyGui: {plugin.Name} {name}");
#endif
                if (container == null) return;
                if (container.plugin != plugin) return;
                if (string.IsNullOrEmpty(name)) return;
                CuiElement target = null;
                foreach (CuiElement element in container)
                {
                    if (element.Parent == name)
                    {
                        CuiHelper.DestroyUi(player, element.Name);
                        garbage.Add(element);
                    }
                    if (element.Name == name) target = element;
                }
                if (target == null) return;
                CuiHelper.DestroyUi(player, target.Name);
                garbage.Add(target);


            }

            public void destroyAllGui(Plugin plugin)
            {
                foreach (GuiContainer container in activeGuiContainers)
                {
                    if (container.plugin != plugin) continue;
                    foreach (Timer timer in container.timers)
                    {
                        timer.Destroy();
                    }
                    foreach (CuiElement element in container)
                    {
                        CuiHelper.DestroyUi(player, element.Name);
                    }
                }
            }

            public void destroyAllGui()
            {
                foreach (GuiContainer container in activeGuiContainers)
                {
                    foreach (Timer timer in container.timers)
                    {
                        timer.Destroy();
                    }
                    foreach (CuiElement element in container)
                    {
                        CuiHelper.DestroyUi(player, element.Name);
                    }
                }
                Destroy(this);
            }
        }

        #endregion classes

        #region API

        public enum gametipType { gametip, warning, error }

        public void customGameTip(BasePlayer player, string text, float duration = 0, gametipType type = gametipType.gametip)
        {
            GuiTracker.getGuiTracker(player).destroyGui(this, "gameTip");

            GuiContainer container = new GuiContainer(this, "gameTip");
            switch (type)
            {
                case gametipType.gametip:
                    container.addImage("gametip", new Rectangle(375, 844, 1170, 58, 1920, 1080, true), "bgTex", GuiContainer.Layer.menu, new GuiColor("#25639BF0"), 0.5f, 1);
                    container.addText("gametip_txt", new Rectangle(433, 844, 1112, 58, 1920, 1080, true), GuiContainer.Layer.menu, new GuiText(text, 20, new GuiColor("#FFFFFFD9")), 0.5f, 1);
                    container.addImage("gametip_icon", new Rectangle(375, 844, 58, 58, 1920, 1080, true), "gameTipIcon", GuiContainer.Layer.menu, new GuiColor("#FFFFFFD9"), 0.5f, 1);
                    break;

                case gametipType.warning:
                    container.addPlainPanel("gametip", new Rectangle(375, 844, 1170, 58, 1920, 1080, true), GuiContainer.Layer.menu, new GuiColor("#DED502F0"), 0.5f, 1);
                    container.addText("gametip_txt", new Rectangle(433, 844, 1112, 58, 1920, 1080, true), GuiContainer.Layer.menu, new GuiText(text, 20, new GuiColor("#000000D9")), 0.5f, 1);
                    container.addImage("gametip_icon", new Rectangle(375, 844, 58, 58, 1920, 1080, true), "warning_alpha", GuiContainer.Layer.menu, new GuiColor("#FFFFFFD9"), 0.5f, 1);
                    break;

                case gametipType.error:
                    container.addImage("gametip", new Rectangle(375, 844, 1170, 58, 1920, 1080, true), "bgTex", GuiContainer.Layer.menu, new GuiColor("#BB0000F0"), 0.5f, 1);
                    container.addText("gametip_txt", new Rectangle(433, 844, 1112, 58, 1920, 1080, true), GuiContainer.Layer.menu, new GuiText(text, 20, new GuiColor("#FFFFFFD9")), 0.5f, 1);
                    container.addImage("gametip_icon", new Rectangle(375, 844, 58, 58, 1920, 1080, true), "white_cross", GuiContainer.Layer.menu, new GuiColor("#FFFFFFD9"), 0.5f, 1);
                    break;
            }

            if (duration != 0)
            {
                Timer closeTimer = timer.Once(duration, () =>
                {
                    GuiTracker.getGuiTracker(player).destroyGui(this, container);
                });
                container.timers.Add(closeTimer);
            }
            container.display(player);
        }

        public void registerImage(Plugin plugin, string name, string url, Action callback = null)
        {
            ImageLibrary.Call("AddImage", url, $"{plugin.Name}_{name}", (ulong)0, callback);
#if DEBUG
            PrintToChat($"{plugin.Name} registered {name} image");
#endif
        }

        #endregion API

        #region helpers

        private string encodeName(GuiContainer container, string name)
        {
            if (GuiContainer.layers.Contains(name)) return name;
            return $"{container.name}_{safeName(name)}";
        }

        private string decodeName(GuiContainer container, string name)
        {
            if (GuiContainer.layers.Contains(name)) return name;
            return name.Substring(container.name.Length + 1);
        }

        public string getItemIcon(string shortname)
        {
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
            if (itemDefinition != null) return ImageLibrary.Call<string>("GetImage", shortname);
            else return "";
        }

        private string getImageData(Plugin plugin, string name)
        {
            return (string)PluginInstance.ImageLibrary.Call("GetImage", $"{plugin.Name}_{name}");
        }

        private static string safeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Regex.Replace(name, " ", "_");
        }
        #endregion helpers

        #region commands

        private void closeUi(ConsoleSystem.Arg arg)
        {
            GuiTracker.getGuiTracker(arg.Player()).destroyAllGui();
        }

        private void listContainers(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args != null) player = BasePlayer.FindByID(ulong.Parse(arg.Args[0]));
            if (player == null) return;
            GuiTracker tracker = GuiTracker.getGuiTracker(player);
            if (tracker.activeGuiContainers.Count == 0)
            {
                SendReply(arg, "you don't have any active guiContainers!");
                return;
            }
            foreach (GuiContainer container in tracker.activeGuiContainers)
            {
                SendReply(arg, $"Plugin: {container.plugin.Name}, Container: {container.name}, Parent: {container.parent}:");
                foreach (CuiElement element in container)
                {
                    SendReply(arg, $"- Element: {element.Name}, Parent: {element.Parent}");
                }
            }
        }

        private void OnGuiInput(ConsoleSystem.Arg arg)
        {
            //gui.input pluginName containerName inputName --close elementNames... --input text...
            if (arg.Args.Length < 3)
            {
                SendReply(arg, "OnGuiInput: not enough arguments given!");
                return;
            }

            BasePlayer player = arg.Player();
            if (player == null) return;

#if DEBUG
            player.ChatMessage($"OnGuiInput: gui.input {arg.FullString}");
#endif

            GuiTracker tracker = GuiTracker.getGuiTracker(player);

            #region parsing
            Stack<string> args = new Stack<string>(arg.Args.Reverse<string>());

            Plugin plugin = Manager.GetPlugin(args.Pop());
            if (plugin == null)
            {
                SendReply(arg, "OnGuiInput: Plugin not found!");
                return;
            }

            GuiContainer container = tracker.getContainer(plugin, args.Pop());
            if (container == null)
            {
                SendReply(arg, "OnGuiInput: Container not found!");
                return;
            }

            string inputName = args.Pop();

            bool closeContainer = false;
            if (inputName == encodeName(container, "close") || inputName == encodeName(container, "close_btn")) closeContainer = true;

            List<string> close = new List<string>();
            List<string> input = new List<string>();
            List<string> select = null;
            string next;

            while (args.TryPop(out next))
            {
                if (next == "--close") select = close;
                else if (next == "--input") select = input;
                else
                {
                    if (select == null)
                    {
                        SendReply(arg, $"OnGuiInput: Couldn't interpret {next}");
                        continue;
                    }
                    else select.Add(next);
                }
            }
            #endregion

            #region execution

            if (!container.runCallback(inputName, player, input.ToArray()))
            {
#if DEBUG
                SendReply(arg, $"OnGuiInput: Callback for {plugin.Name} {container.name} {inputName} wasn't found");
#endif
            }

            if (close.Count != 0)
            {
                foreach (string name in close)
                {
                    tracker.destroyGui(plugin, container, name);
                }
            }

            if (closeContainer) tracker.destroyGui(plugin, container);

            #endregion

        }


        [ChatCommand("guidemo")]
        private void demoCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "gui.demo"))
            {
                PrintToChat(player, lang.GetMessage("noPermission", this, player.UserIDString));
                return;
            }
            if (args.Length == 0)
            {
                GuiContainer container = new GuiContainer(this, "demo");
                container.addPanel("demo_panel", new Rectangle(0.25f, 0.5f, 0.25f, 0.25f), GuiContainer.Layer.hud, new GuiColor(0, 1, 0, 1), 1, 1, new GuiText("This is a regular panel", 30));
                container.addPanel("demo_img", new Rectangle(0.25f, 0.25f, 0.25f, 0.25f), FadeIn: 1, FadeOut: 1, text: new GuiText("this is an image with writing on it", 30, color: new GuiColor(1, 1, 1, 1)), imgName: "flower");
                Action<BasePlayer, string[]> heal = (bPlayer, input) => { bPlayer.Heal(10); };
                container.addButton("demo_healButton", new Rectangle(0.5f, 0.5f, 0.25f, 0.25f), GuiContainer.Layer.hud, null, 1, 1, new GuiText("heal me", 40), heal, null, false, "flower");
                Action<BasePlayer, string[]> hurt = (bPlayer, input) => { bPlayer.Hurt(10); };
                container.addButton("demo_hurtButton", new Rectangle(0.5f, 0.25f, 0.25f, 0.25f), new GuiColor(1, 0, 0, 0.5f), 1, 1, new GuiText("hurt me", 40), hurt);
                container.addText("demo_inputLabel", new Rectangle(0.375f, 0.85f, 0.25f, 0.1f), new GuiText("Print to chat:", 50), 1, 1);
                Action<BasePlayer, string[]> inputCallback = (bPlayer, input) => { PrintToChat(bPlayer, string.Concat(input)); };
                container.addInput("demo_input", new Rectangle(0.375f, 0.75f, 0.25f, 0.1f), inputCallback, GuiContainer.Layer.hud, null, new GuiColor("white"), 100, new GuiText("", 50), 1, 1);
                container.addButton("close", new Rectangle(0.1f, 0.1f, 0.1f, 0.1f), new GuiColor("red"), 1, 1, new GuiText("close", 50));
                container.display(player);

                GuiContainer container2 = new GuiContainer(this, "demo_child", "demo");
                container2.addPanel("layer1", new Rectangle(1400, 800, 300, 100, 1920, 1080, true), GuiContainer.Layer.overall, new GuiColor("red"), 1, 1, new GuiText("overall", align: TextAnchor.LowerLeft));
                container2.addPanel("layers_label", new Rectangle(0, 0, 1, 1), "layer1", null, 1, 1, new GuiText("Available layers:", 20, align: TextAnchor.UpperLeft));
                container2.addPanel("layer2", new Rectangle(1425, 825, 300, 100, 1920, 1080, true), GuiContainer.Layer.overlay, new GuiColor("yellow"), 1, 1, new GuiText("overlay", align: TextAnchor.LowerLeft));
                container2.addPanel("layer3", new Rectangle(1450, 850, 300, 100, 1920, 1080, true), GuiContainer.Layer.menu, new GuiColor("green"), 1, 1, new GuiText("menu", align: TextAnchor.LowerLeft));
                container2.addPanel("layer4", new Rectangle(1475, 875, 300, 100, 1920, 1080, true), GuiContainer.Layer.hud, new GuiColor("blue"), 1, 1, new GuiText("hud", align: TextAnchor.LowerLeft));
                container2.addPanel("layer5", new Rectangle(1500, 900, 300, 100, 1920, 1080, true), GuiContainer.Layer.under, new GuiColor("purple"), 1, 1, new GuiText("under", align: TextAnchor.LowerLeft));
                container2.display(player);

                customGameTip(player, "This is a custom gametip!", 5);
                return;
            }
            else if (args.Length >= 3)
            {
                if (args[0] == "gametip") customGameTip(player, args[2], 3f, (gametipType)Enum.Parse(typeof(gametipType), args[1]));
            }
        }

        [ChatCommand("img")]
        private void imgPreviewCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "gui.demo"))
            {
                PrintToChat(player, lang.GetMessage("noPermission", this, player.UserIDString));
                return;
            }
            imgPreview(player, args[0]);
        }

        public void imgPreview(BasePlayer player, string url)
        {
            Action callback = () =>
            {
                Rectangle rectangle = new Rectangle(710, 290, 500, 500, 1920, 1080, true);
                GuiContainer container = new GuiContainer(PluginInstance, $"imgPreview");
                container.addRawImage($"img", rectangle, ImageLibrary.Call<string>("GetImage", $"GUICreator_preview_{url}"), "Hud");
                container.addPlainButton("close", new Rectangle(0.15f, 0.15f, 0.1f, 0.1f), new GuiColor(1, 0, 0, 0.8f), 0, 0, new GuiText("close"));
                container.display(player);
            };
            if (ImageLibrary.Call<bool>("HasImage", $"GUICreator_preview_{url}", (ulong)0))
            {
                callback();
            }
            else ImageLibrary.Call<bool>("AddImage", url, $"GUICreator_preview_{url}", (ulong)0, callback);

        }

        [ChatCommand("imgraw")]
        private void imgrawPreviewCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "gui.demo"))
            {
                PrintToChat(player, lang.GetMessage("noPermission", this, player.UserIDString));
                return;
            }
            GuiContainer container = new GuiContainer(this, "imgPreview");

            container.addRawImage("img", new Rectangle(710, 290, 500, 500, 1920, 1080, true), args[0], GUICreator.GuiContainer.Layer.hud);
            container.addPlainButton("close", new Rectangle(0.15f, 0.15f, 0.1f, 0.1f), new GuiColor(1, 0, 0, 0.8f), 0, 0, new GuiText("close"));
            container.display(player);
        }


        #endregion commands

        #region Config

        private static ConfigData config;

        private class ConfigData
        {
            public string gameTipIcon;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                gameTipIcon = "https://i.imgur.com/VFBB8ib.png"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Config

        #region Localization

        private Dictionary<string, string> messages = new Dictionary<string, string>()
    {
        {"noPermission", "You don't have permission to use this command!"}
    };

        #endregion Localization
    }
}