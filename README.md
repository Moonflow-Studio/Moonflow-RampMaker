# MFRampMaker
■■Finished■■A total solution of making ramp texture for your game developing. You can use this to create multiple ribbon of gradient color to read by one texture conveniently, and the plugin also support to preview the final result of effect on target material on the real time.

[Realtime link debugging on Moonflow Ramp Maker - YouTube](https://www.youtube.com/watch?v=7q9NxYDcCi4)

![](https://raw.githubusercontent.com/Reguluz/ImageBed/master/default_panning1.png)

After install the plugin, you can find it on Menu bar - Moonflow - Tools - Art - MFRampMaker to open an instance panel.

You can also select a editable material, right click on it's inspector panel to find "Link To Ramp Maker" to open it, and this way will link material to plugin instance automatically.

![](https://raw.githubusercontent.com/Reguluz/ImageBed/master/MatLink2RampMaker.png)

**What's on the panel:**

### Properties

1. **Mixing Mode** - Chosen if you need to mixing color between nearly ribbons. It's useful when you need to make gradient color not only on horizontal but also on vertical of texture

   ![](https://raw.githubusercontent.com/Reguluz/ImageBed/master/MixingMode.png)

2. **Gamma Mode** - Chosen if you need to create an sRGB Texture to make correct color display on gamma space

   ![](https://raw.githubusercontent.com/Reguluz/ImageBed/master/GammaMode.png)

3. **Ribbon Num** - The maximum number of ribbon we support on one texture is 8

4. **ReadConfig / Save Config** - You can save the ribbon settings as an asset in your project to reuse in other ways, or you just want to save a backup version of your configs. What you saved is looked like this

   ​	![](https://raw.githubusercontent.com/Reguluz/ImageBed/master/GradingConfig.png)

5. **Target property** (and target material on the top) - You can link the plugin instance to any editable material and choose a texture property. After you tap "Link to target property", a render texture will be created for preview result on real time by covering the old texture you set to the material before. 

   ![](https://raw.githubusercontent.com/Reguluz/ImageBed/master/ChooseProperty.png)

   ![](https://raw.githubusercontent.com/Reguluz/ImageBed/master/LinkToProperty.png)

   Don't worry if you want to recover the old setting. You can tap "Break Link" and the old texture will be recovered again.

### Texture Preview Settings

1. **Resolution Preview Level** - Set the texture size level, which based on the power of 2. You can see the result size of texture on the second line. We support size from 32 pixels per ribbon to 512 pixels per ribbon(level 0 to 4), and each ribbon has 2 pixels height.
2. **Save Texture As** - After you finish debugging all the settings, you can tap this to save your texture in your project as an PNG file. Notice that we made a limit that you can only save the texture under "_Assets/_" path.

