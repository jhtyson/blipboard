export interface Blip {
  level: string;
  lane: string;

  timeBegin: number;
  timeEnd: number;

  count: number;
  body: string;

  details: string; // deprecated
}

export interface BlipBoardTarget {
  addBlip(blip: Blip): void;
}

export interface ScaleSettings {
  timeBegin: number;
  timeEnd: number;

  displayBegin: number;
  displayEnd: number;

  blipSize: number;
}

export interface Context {
  time: number;
  onClick: (blip: Blip) => void;
  onHover: (blip?: Blip) => void;
}

export interface BlipBoardConnection {
  startConnection(target: BlipBoardTarget): void;
}
