using System;
using System.Threading.Tasks;

namespace Ascon.Pilot.SDK.CreateObjectSample;

public class ObjectLoader : IObserver<IDataObject>
{
	private readonly IObjectsRepository _repository;

	private IDisposable _subscription;

	private TaskCompletionSource<IDataObject> _tcs;

	private long _changesetId;

	public ObjectLoader(IObjectsRepository repository)
	{
		_repository = repository;
	}

	public Task<IDataObject> Load(Guid id, long changesetId = 0L)
	{
		_changesetId = changesetId;
		_tcs = new TaskCompletionSource<IDataObject>();
		_subscription = _repository.SubscribeObjects(new Guid[1] { id }).Subscribe(this);
		return _tcs.Task;
	}

	public void OnNext(IDataObject value)
	{
		if (value.State == DataState.Loaded && value.LastChange() >= _changesetId)
		{
			_tcs.TrySetResult(value);
			_subscription.Dispose();
		}
	}

	public void OnError(Exception error)
	{
	}

	public void OnCompleted()
	{
	}
}
