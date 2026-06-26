using Winithm.Core.Common;
using Winithm.Core.Managers;

namespace Winithm.Core.Interfaces;

public interface IDeepCloneableUID<T>
{
  T DeepCloner(ObjectFactory objectFactory, BeatTime? offset);
}

public interface IDeepCloneableStatic<T>
{
  T DeepCloner();
}