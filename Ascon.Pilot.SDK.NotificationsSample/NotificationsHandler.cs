using System;
using System.ComponentModel.Composition;
using System.Text;

namespace Ascon.Pilot.SDK.NotificationsSample;

[Export(typeof(INotificationsHandler))]
public class NotificationsHandler : INotificationsHandler
{
	private readonly IObjectsRepository _repository;

	[ImportingConstructor]
	public NotificationsHandler(IObjectsRepository repository)
	{
		_repository = repository;
	}

	[Obsolete]
	public bool Handle(INotification notification)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (notification.UserId.HasValue)
		{
			stringBuilder.Append(_repository.GetPerson(notification.UserId.Value).DisplayName);
		}
		stringBuilder.Append(" " + notification.GetActionString());
		stringBuilder.Append(" " + notification.Title);
		return false;
	}
}
