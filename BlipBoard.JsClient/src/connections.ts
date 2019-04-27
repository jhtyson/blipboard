import signalR from "@aspnet/signalr";
import { BlipBoardTarget, BlipBoardConnection, Blip } from "./common";

export function getConnection(): BlipBoardConnection | undefined {
  const urlParams = new URLSearchParams(window.location.search);
  const signalRBoardUrl = urlParams.get("signalRUrl");
  const pollingBaseUrl = urlParams.get("pollingBaseUrl");
  const boardId = urlParams.get("boardId");
  if (signalRBoardUrl) {
    return new SignalRConnection(signalRBoardUrl);
  } else if (pollingBaseUrl && boardId) {
    return new PollingConnection(pollingBaseUrl, boardId);
  } else if (boardId) {
    return new PollingConnection("/api/feed", boardId);
  } else {
    return undefined;
  }
}

class SignalRConnection implements BlipBoardConnection {
  constructor(private boardUrl: string) {}

  startConnection(target: BlipBoardTarget) {
    console.info("Starting SignalR connection at " + this.boardUrl);

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(this.boardUrl)
      .configureLogging(signalR.LogLevel.Trace)
      .build();

    connection.on("AddAsync", e => target.addBlip(e));

    connection.start();
  }
}

function delay(millis: number) {
  return new Promise<void>((resolve, reject) => setTimeout(resolve, millis));
}

class PollingConnection implements BlipBoardConnection {
  target: BlipBoardTarget | undefined = undefined;
  lastBlipFrom = 0;

  constructor(private baseUrl: string, private boardId: string) {}

  async startConnection(target: BlipBoardTarget) {
    if (this.target) throw Error("PollingConnection already has a target");

    console.info("Starting polling connection at " + this.baseUrl);

    this.target = target;

    var result = await fetch(`${this.baseUrl}/all?id=${this.boardId}`);

    this.addBlipsFromObject(await result.json());

    await this.beginWorkLoop();
  }

  async beginWorkLoop() {
    while (true) {
      await delay(500);

      var result = await fetch(
        `${this.baseUrl}/latest?id=${this.boardId}&since=${this.lastBlipFrom}`
      );

      this.addBlipsFromObject(await result.json());
    }
  }

  addBlipsFromObject(thing: any) {
    this.addBlips(thing as Blip[]);
  }

  addBlips(blips: Blip[]) {
    for (var i = 0; i < blips.length; ++i) {
      const blip = blips[i];
      this.target!.addBlip(blip);
      if (blip.timeBegin > this.lastBlipFrom) {
        this.lastBlipFrom = blip.timeBegin;
      }
    }
  }
}
