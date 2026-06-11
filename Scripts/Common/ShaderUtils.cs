using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Winithm.Core.Common;

public struct ShaderParamDef
{
  public AnyValueType Type;
  public AnyValue DefaultValue;

  public ShaderParamDef(AnyValueType type, AnyValue defaultValue)
  {
    Type = type;
    DefaultValue = defaultValue;
  }
}

/// <summary>
/// Data structure representing a user-defined uniform in a shader.
/// </summary>
public struct ShaderUniform
{
  public string Name;
  public string Type;
  public string RawDefaultValue;

  public override string ToString() => $"{Type} {Name} (Default: {RawDefaultValue ?? "None"})";
}

/// <summary>
/// Utilities for processing GLSL shader code for the Winithm engine.
/// </summary>
public static partial class ShaderUtils
{
  [GeneratedRegex(@"uniform\s+(\w+)\s+(\w+)\s*;(?:\s*//\s*%\s*(.*?)\s*%)?", RegexOptions.Multiline)]
  private static partial Regex ShaderUniformRegex();

  /// <summary>
  /// Maps a GLSL type name to its corresponding AnyValueType.
  /// </summary>
  public static AnyValueType GlslTypeToAnyValueType(string glslType)
  {
    return glslType switch
    {
      "float" => AnyValueType.Float,
      "vec2" => AnyValueType.Vec2,
      "vec3" => AnyValueType.Vec3,
      "vec4" => AnyValueType.Vec4,
      "bool" => AnyValueType.Bool,
      "sampler2D" => AnyValueType.String,
      _ => AnyValueType.Float,
    };
  }

  /// <summary>
  /// Built-in variables that should be ignored during parameter mapping.
  /// These are provided by the engine/viewport.
  /// </summary>
  public static readonly HashSet<string> SystemUniforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      "ScreenSize",
      "ScreenTexture",
      "Time",
      "Model",
      "Projection",
      "UVScale"
    };

  /// <summary>
  /// Extracts full user-defined uniform information from GLSL source code,
  /// excluding system-standard built-in uniforms.
  /// Order is preserved based on appearance in the source code.
  /// </summary>
  public static List<ShaderUniform> ParseUserUniforms(string shaderCode)
  {
    var uniforms = new List<ShaderUniform>();

    // Combined Regex:
    // 1. Matches: uniform <type> <name>;
    // 2. Optionally matches: // %default% (at the end of the line)
    // Groups: 1=Type, 2=Name, 3=Default (optional)
    var regex = ShaderUniformRegex();

    var matches = regex.Matches(shaderCode);
    foreach (Match match in matches)
    {
      string type = match.Groups[1].Value;
      string name = match.Groups[2].Value;
      string defaultValue = match.Groups[3].Success ? match.Groups[3].Value : null;

      if (!SystemUniforms.Contains(name))
      {
        uniforms.Add(new ShaderUniform
        {
          Name = name,
          Type = type,
          RawDefaultValue = defaultValue
        });
      }
    }

    return uniforms;
  }

  /// <summary>
  /// Legacy wrapper for getting just names.
  /// </summary>
  public static List<string> GetUserUniformNames(string shaderCode)
  {
    var result = new List<string>();
    foreach (var u in ParseUserUniforms(shaderCode))
      result.Add(u.Name);
    return result;
  }

}
