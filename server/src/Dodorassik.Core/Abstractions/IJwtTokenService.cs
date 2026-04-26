using Dodorassik.Core.Domain;

namespace Dodorassik.Core.Abstractions;

public interface IJwtTokenService
{
    string Issue(User user);
}
