using Microsoft.Maui.Controls;

namespace CosmicMusic.Helpers
{
    public static class AnimationHelper
    {
        public static readonly BindableProperty IsBreathingProperty =
            BindableProperty.CreateAttached(
                "IsBreathing",
                typeof(bool),
                typeof(AnimationHelper),
                false,
                propertyChanged: OnIsBreathingChanged);

        public static bool GetIsBreathing(BindableObject view) => (bool)view.GetValue(IsBreathingProperty);
        public static void SetIsBreathing(BindableObject view, bool value) => view.SetValue(IsBreathingProperty, value);

        private static void OnIsBreathingChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is VisualElement view)
            {
                bool isPlaying = (bool)newValue;
                if (isPlaying)
                {
                   
                    view.AbortAnimation("NeonGlowAnim");

                   
                    if (view.Shadow != null)
                    {
                        var parentAnimation = new Animation();

                      
                        var glowOut = new Animation(v =>
                        {
                            view.Shadow.Radius = (float)v;
                            view.Shadow.Opacity = (float)(v / 25);
                        }, 5, 25, Easing.CubicOut);

                        
                        var glowIn = new Animation(v =>
                        {
                            view.Shadow.Radius = (float)v;
                            view.Shadow.Opacity = (float)(v / 25);
                        }, 25, 5, Easing.CubicIn);

                       
                        parentAnimation.Add(0, 0.5, glowOut);
                        parentAnimation.Add(0.5, 1, glowIn);

                     
                        parentAnimation.Commit(view, "NeonGlowAnim", length: 1500, repeat: () => true);
                    }
                    else
                    {
                       
                        var scaleUp = new Animation(v => view.Scale = v, 1.0, 1.03, Easing.CubicInOut);
                        var scaleDown = new Animation(v => view.Scale = v, 1.03, 1.0, Easing.CubicInOut);
                        var anim = new Animation { { 0, 0.5, scaleUp }, { 0.5, 1, scaleDown } };
                        anim.Commit(view, "NeonGlowAnim", length: 1500, repeat: () => true);
                    }
                }
                else
                {
                    
                    view.AbortAnimation("NeonGlowAnim");

                    if (view.Shadow != null)
                    {
                        view.Shadow.Radius = 5;
                        view.Shadow.Opacity = 0.8f;
                    }
                    else
                    {
                        view.ScaleTo(1.0, 250, Easing.CubicOut);
                    }
                }
            }
        }
    }
}