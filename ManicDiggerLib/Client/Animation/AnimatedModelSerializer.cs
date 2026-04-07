/// <summary>
/// Handles serialization and deserialization of <see cref="AnimatedModel"/>
/// to and from the tab-separated section format via <see cref="TableSerializer"/>.
/// </summary>
public class AnimatedModelSerializer
{
    /// <summary>
    /// Deserializes an <see cref="AnimatedModel"/> from a tab-separated
    /// section-format string.
    /// </summary>
    /// <param name="p">Platform utilities for string and float parsing.</param>
    /// <param name="data">The raw text content to deserialize.</param>
    /// <returns>A fully populated <see cref="AnimatedModel"/>.</returns>
    public static AnimatedModel Deserialize(GamePlatform p, string data)
    {
        AnimatedModel model = new()
        {
            nodes = [],
            Keyframes = [],
            Animations = []
        };
        AnimatedModelBinding b = new()
        {
            p = p,
            m = model
        };
        TableSerializer.Deserialize(p, data, b);
        return model;
    }
}
