using SyZero.Dependency;
using SyZero.Runtime.Security;
using SyZero.Runtime.Session;
using SyZero.Util;

namespace SyZero.Application.Service
{
    public class ApplicationService : SyZeroServiceBase, IApplicationService
    {
        public ISySession SySession => SyZeroUtil.GetScopeService<ISySession>();

        protected virtual void CheckPermission(string permissionName = null)
        {
            if (!SySession.UserId.HasValue)
            {
                throw new SyMessageException(SyMessageBoxStatus.UnAuthorized);
            }
            if (!string.IsNullOrEmpty(permissionName))
            {
                PermissionChecker.Authorize(permissionName);
            }
        }
    }

}
