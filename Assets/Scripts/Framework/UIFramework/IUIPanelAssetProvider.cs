using System.Threading;
using System.Threading.Tasks;

namespace Framework.UIFramework
{
    public interface IUIPanelAssetProvider
    {
        Task<UIPanelAssetLease> LoadAsync(UIPanelDescriptor descriptor,CancellationToken cancellationToken=default);
    }
}
