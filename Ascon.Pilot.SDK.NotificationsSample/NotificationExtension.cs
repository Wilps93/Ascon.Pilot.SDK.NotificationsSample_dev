using System;

namespace Ascon.Pilot.SDK.NotificationsSample;

public static class NotificationExtension
{
	[Obsolete]
	public static string GetActionString(this INotification notification)
	{
		return notification.ChangeKind switch
		{
			NotificationKind.ObjectCreated => Resources.Created, 
			NotificationKind.ObjectDeleted => Resources.Deleted, 
			NotificationKind.ObjectMoved => Resources.Moved, 
			NotificationKind.ObjectRenamed => Resources.Renamed, 
			NotificationKind.ObjectRestored => Resources.Restored, 
			NotificationKind.ObjectAccessChanged => Resources.AccessChanged, 
			NotificationKind.ObjectAttributeChanged => Resources.AttributeChanged, 
			NotificationKind.ObjectFileChanged => Resources.FileChanged, 
			NotificationKind.ObjectSignatureChanged => Resources.SignatureChanged, 
			NotificationKind.ObjectAnnotationAdded => Resources.AnnotationAdded, 
			NotificationKind.ObjectAnnotationDeleted => Resources.AnnotationDeleted, 
			NotificationKind.ObjectAnnotationChanged2 => Resources.AnnotationChanged, 
			NotificationKind.ObjectAnnotationMessageAdded => Resources.AnnotationMessageAdded, 
			NotificationKind.ObjectAnnotationMessageDeleted => Resources.AnnotationMessageDeleted, 
			NotificationKind.ObjectAnnotationMessageChanged => Resources.AnnotationMessageChanged, 
			NotificationKind.TaskCreated => Resources.TaskCreated, 
			NotificationKind.TaskMessageChanged => Resources.MessageCreated, 
			NotificationKind.TaskAttachmentChanged => Resources.TaskAttachmentChanged, 
			NotificationKind.TaskTitleChanged => Resources.TaskTitleChanged, 
			NotificationKind.TaskDescriptionChanged => Resources.TaskDescriptionChanged, 
			NotificationKind.TaskStateAssigned => Resources.StateAssigned, 
			NotificationKind.TaskStateInProgress => Resources.StateInProgress, 
			NotificationKind.TaskStateOnValidation => Resources.StateOnValidation, 
			NotificationKind.TaskStateReturnedAfterValidation => Resources.StateReturnedAfterValidation, 
			NotificationKind.TaskStateRevoked => Resources.StateRevoked, 
			NotificationKind.ObjectUnlocked => Resources.Unlocked, 
			NotificationKind.TaskDeadlineChanged => Resources.DeadlineChanged, 
			NotificationKind.ObjectFreezed => Resources.Freezed, 
			NotificationKind.ObjectUnfreezed => Resources.Unfreezed, 
			_ => string.Empty, 
		};
	}
}
