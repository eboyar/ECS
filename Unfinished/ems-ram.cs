//mostly finished request-a-missile script to be used with EMS Core
//by eboyar

readonly IMyUnicastListener blResponse;
readonly MyCommandLine cL = new MyCommandLine();
readonly Random rnd = new Random();

public struct Response
{
    public long CarrierID;
    public string Distance;
}
public enum RequestStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
public class Request
{
    public int ID { get; set; }
    public string LaunchCommand { get; set; }
    public List<Response> Responses { get; private set; } = new List<Response>();
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    public Request(int id, string launchCommand)
    {
        ID = id;
        LaunchCommand = launchCommand;
    }
}

Queue<Request> launchCommandQueue = new Queue<Request>();

private int updateCounter = 0;

List<Request> requests = new List<Request>();

public Program()
{
    blResponse = IGC.UnicastListener;
    blResponse.SetMessageCallback("LResponse");
}

public void Main(string argument, UpdateType updateSource)
{
    if (cL.TryParse(argument))
    {
        if (cL.ArgumentCount == 3 && cL.Argument(0) == "launch")
        {
            var request = new Request(GenerateUniqueID(), argument);
            launchCommandQueue.Enqueue(request);
            Echo($"Request {request.ID} added to queue");
            Runtime.UpdateFrequency |= UpdateFrequency.Update100;
        }
    }
    if ((updateSource & UpdateType.IGC) != 0)
    {
        while (blResponse.HasPendingMessage)
        {
            var message = blResponse.AcceptMessage();
            Echo($"Received message: {message.Data}");
            if (message.Data is string && message.Tag == "Launch Response")
            {
                var responseParts = message.Data.ToString().Split('|');
                if (responseParts.Length == 2)
                {
                    var carrierID = message.Source;
                    var requestID = int.Parse(responseParts[0]);
                    var distance = responseParts[1];

                    var request = requests.FirstOrDefault(r => r.ID == requestID);
                    if (request != null)
                    {
                        request.Responses.Add(new Response { CarrierID = carrierID, Distance = distance });
                        Echo($"Response added to request {request.ID}");
                    }
                }
            }
        }
    }

    if ((updateSource & UpdateType.Update100) != 0)
    {
        if (updateCounter == 0)
        {
            var currentRequest = requests.FirstOrDefault(r => r.Status == RequestStatus.Processing);
            if (currentRequest == null && launchCommandQueue.Count > 0)
            {
                currentRequest = launchCommandQueue.Dequeue();
                currentRequest.Status = RequestStatus.Processing;
                requests.Add(currentRequest);

                var worldPosition = Me.GetPosition().ToString();
                var message = $"{currentRequest.ID}|{currentRequest.LaunchCommand}|{worldPosition}";
                IGC.SendBroadcastMessage("Launch Request", message, TransmissionDistance.TransmissionDistanceMax);
                Echo($"Request {currentRequest.ID} sent to carriers");
            }
        }

        updateCounter++;
        if (updateCounter == 2)
        {
            var currentRequest = requests.FirstOrDefault(r => r.Status == RequestStatus.Processing);
            if (currentRequest != null && currentRequest.Responses.Count > 0)
            {
                var closestResponse = currentRequest.Responses.OrderBy(resp => double.Parse(resp.Distance)).First();
                var message = $"{currentRequest.LaunchCommand}";
                IGC.SendUnicastMessage(closestResponse.CarrierID, "Launch Command", message);
                Echo($"Final Command sent to carrier {closestResponse.CarrierID}");
                currentRequest.Status = RequestStatus.Completed;
            }

            else if (currentRequest != null && currentRequest.Responses.Count == 0)
            {
                currentRequest.Status = RequestStatus.Failed;
            }

            updateCounter = 0;
            if (launchCommandQueue.Count == 0)
            {
                Runtime.UpdateFrequency &= ~UpdateFrequency.Update100;
            }
        }
    }
}
private int GenerateUniqueID()
{
    int newID;
    do
    {
        newID = rnd.Next(0, 21474836);
    } while (requests.Any(r => r.ID == newID));

    return newID;
}