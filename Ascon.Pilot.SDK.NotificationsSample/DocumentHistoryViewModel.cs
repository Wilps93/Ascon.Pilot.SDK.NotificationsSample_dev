using System;

namespace Ascon.Pilot.SDK.NotificationsSample;

internal class DocumentHistoryViewModel
{
	private Guid selectedId;

	private IObjectsRepository repository;

	private IObjectModifier modifier;

	private object dispatcher;

	public DocumentHistoryViewModel(Guid selectedId, IObjectsRepository repository, IObjectModifier modifier, object dispatcher)
	{
		this.selectedId = selectedId;
		this.repository = repository;
		this.modifier = modifier;
		this.dispatcher = dispatcher;
	}
}
