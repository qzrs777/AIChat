using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

public static class EmbeddedSpriteLoader
{
    public static Sprite Load(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        string fullName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceName));

        if (fullName == null)
            throw new Exception($"找不到嵌入资源: {resourceName}");

        using (var stream = assembly.GetManifestResourceStream(fullName))
        {
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            ImageConversion.LoadImage(tex, data);

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f)
            );
        }
    }
}
