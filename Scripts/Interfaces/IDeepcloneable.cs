using Winithm.Core.Common;
using Winithm.Core.Managers;

namespace Winithm.Core.Interfaces;

public interface IDeepCloneable<T>
{
  T DeepClone(ObjectFactory objectFactory, BeatTime? offset);
}
