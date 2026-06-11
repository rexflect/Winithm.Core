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
/// Data structure representing a user-defined uniform in a GDShader.
/// </summary>
public struct ShaderUniform
{
  public string Name;
  public string Type;
  /// <summary>
  /// Hint string as written in source, e.g. "hint_range(0.0, 1.0)" or "source_color".
  /// Null when no hint is present.
  /// </summary>
  public string Hint;
  /// <summary>
  /// Raw default value as written in source, e.g. "1.0", "vec4(1.0)", "true".
  /// Null when no default is specified.
  /// </summary>
  public string RawDefaultValue;

  public override readonly string ToString() =>
    $"{Type} {Name}" +
    (Hint != null ? $" : {Hint}" : "") +
    (RawDefaultValue != null ? $" = {RawDefaultValue}" : "");
}

/// <summary>
/// Utilities for processing GDShader source code for the Winithm engine.
/// </summary>
public static partial class ShaderUtils
{
  // GDShader uniform syntax:
  //   uniform <type> <name> [: <hint>] [= <default_expr>] ;
  [GeneratedRegex(
    @"uniform\s+(\w+)\s+(\w+)" +          // uniform <type> <name>
    @"(?:\s*:\s*([\w()\s.,+-]+?))?" +      // optional  : <hint>
    @"(?:\s*=\s*([^;]+?))?" +             // optional  = <default>
    @"\s*;",
    RegexOptions.Multiline)]
  private static partial Regex ShaderUniformRegex();

  /// <summary>
  /// Maps a GDShader type name to its corresponding AnyValueType.
  /// </summary>
  public static AnyValueType GlslTypeToAnyValueType(string gdType) =>
    gdType switch
    {
      "float" => AnyValueType.Float,
      "vec2" => AnyValueType.Vec2,
      "vec3" => AnyValueType.Vec3,
      "vec4" => AnyValueType.Vec4,
      "bool" => AnyValueType.Bool,
      "sampler2D" => AnyValueType.String,   // resolved to texture path at runtime
      _ => AnyValueType.Float,
    };

  /// <summary>
  /// Extracts all user-defined uniforms from GDShader source code.
  /// Godot built-ins are not declared as uniforms, so no filtering is required.
  /// Order is preserved based on appearance in the source.
  /// </summary>
  public static List<ShaderUniform> ParseUserUniforms(string shaderCode)
  {
    var uniforms = new List<ShaderUniform>();
    foreach (Match match in ShaderUniformRegex().Matches(shaderCode))
    {
      uniforms.Add(new ShaderUniform
      {
        Type = match.Groups[1].Value,
        Name = match.Groups[2].Value,
        Hint = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null,
        RawDefaultValue = match.Groups[4].Success ? match.Groups[4].Value.Trim() : null,
      });
    }
    return uniforms;
  }

  /// <summary>
  /// Legacy wrapper — returns only uniform names.
  /// </summary>
  public static List<string> GetUserUniformNames(string shaderCode)
  {
    var result = new List<string>();
    foreach (var u in ParseUserUniforms(shaderCode))
      result.Add(u.Name);
    return result;
  }
}