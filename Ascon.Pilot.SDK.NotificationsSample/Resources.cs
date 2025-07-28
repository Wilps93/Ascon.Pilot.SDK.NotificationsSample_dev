using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace Ascon.Pilot.SDK.NotificationsSample;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class Resources
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				ResourceManager temp = new ResourceManager("Ascon.Pilot.SDK.NotificationsSample.Resources", typeof(Resources).Assembly);
				resourceMan = temp;
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	internal static string AccessChanged => ResourceManager.GetString("AccessChanged", resourceCulture);

	internal static string AnnotationAdded => ResourceManager.GetString("AnnotationAdded", resourceCulture);

	internal static string AnnotationChanged => ResourceManager.GetString("AnnotationChanged", resourceCulture);

	internal static string AnnotationDeleted => ResourceManager.GetString("AnnotationDeleted", resourceCulture);

	internal static string AnnotationMessageAdded => ResourceManager.GetString("AnnotationMessageAdded", resourceCulture);

	internal static string AnnotationMessageChanged => ResourceManager.GetString("AnnotationMessageChanged", resourceCulture);

	internal static string AnnotationMessageDeleted => ResourceManager.GetString("AnnotationMessageDeleted", resourceCulture);

	internal static string AttributeChanged => ResourceManager.GetString("AttributeChanged", resourceCulture);

	internal static string Created => ResourceManager.GetString("Created", resourceCulture);

	internal static string DeadlineChanged => ResourceManager.GetString("DeadlineChanged", resourceCulture);

	internal static string Deleted => ResourceManager.GetString("Deleted", resourceCulture);

	internal static string FileChanged => ResourceManager.GetString("FileChanged", resourceCulture);

	internal static string Freezed => ResourceManager.GetString("Freezed", resourceCulture);

	internal static string MessageCreated => ResourceManager.GetString("MessageCreated", resourceCulture);

	internal static string Moved => ResourceManager.GetString("Moved", resourceCulture);

	internal static string Renamed => ResourceManager.GetString("Renamed", resourceCulture);

	internal static string Restored => ResourceManager.GetString("Restored", resourceCulture);

	internal static string SignatureChanged => ResourceManager.GetString("SignatureChanged", resourceCulture);

	internal static string StateAssigned => ResourceManager.GetString("StateAssigned", resourceCulture);

	internal static string StateInProgress => ResourceManager.GetString("StateInProgress", resourceCulture);

	internal static string StateOnValidation => ResourceManager.GetString("StateOnValidation", resourceCulture);

	internal static string StateReturnedAfterValidation => ResourceManager.GetString("StateReturnedAfterValidation", resourceCulture);

	internal static string StateRevoked => ResourceManager.GetString("StateRevoked", resourceCulture);

	internal static string TaskAttachmentChanged => ResourceManager.GetString("TaskAttachmentChanged", resourceCulture);

	internal static string TaskCreated => ResourceManager.GetString("TaskCreated", resourceCulture);

	internal static string TaskDescriptionChanged => ResourceManager.GetString("TaskDescriptionChanged", resourceCulture);

	internal static string TaskTitleChanged => ResourceManager.GetString("TaskTitleChanged", resourceCulture);

	internal static string Unfreezed => ResourceManager.GetString("Unfreezed", resourceCulture);

	internal static string Unlocked => ResourceManager.GetString("Unlocked", resourceCulture);

	internal Resources()
	{
	}
}
