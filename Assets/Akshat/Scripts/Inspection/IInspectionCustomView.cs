using Akshat.Interaction;

namespace Akshat.Inspection
{
    public interface IInspectionCustomView
    {
        void Bind(IInspectable inspectable, InspectionManager manager);
        void Unbind();
    }
}
