import { Blip, Context, ScaleSettings } from "./common";

const svgNS = "http://www.w3.org/2000/svg";

interface LocalBlip extends Blip {
  element: any;

  y: number;
  h: number;
}

class Lane {
  group = document.createElementNS(svgNS, "g");
  background = document.createElementNS(svgNS, "rect");

  channels: { [level: string]: Channel } = {};

  constructor(private board: Board, private context: Context) {
    const s = board.scale.settings;
    this.background.classList.add("lane");
    this.background.setAttribute("width", "1");
    this.background.setAttribute(
      "height",
      (s.displayEnd - s.displayBegin).toString()
    );
    this.group.appendChild(this.background);

    // Precreate the channels to have them in the correct order
    for (let i = 0; i < 6; ++i) {
      this.makeChannel(i.toString());
    }
  }

  add(blip: Blip) {
    let channel = this.channels[blip.level];
    if (!channel) {
      channel = this.makeChannel(blip.level);
    }

    channel.add(blip);
  }

  update() {
    for (var c in this.channels) {
      this.channels[c].updateAll();
    }
  }

  private makeChannel(level: string) {
    const channel = (this.channels[level] = new Channel(
      this.board,
      this.context
    ));

    this.group.appendChild(channel.group);
    channel.group.dataset.level = level;

    return channel;
  }
}

class Channel {
  group = document.createElementNS(svgNS, "g");

  private blips: LocalBlip[] = [];

  constructor(private board: Board, private context: Context) {}

  getLatestBlip() {
    return this.blips[this.blips.length - 1];
  }

  add(blip: Blip) {
    this.makeBlipElement(blip as LocalBlip);
  }

  private makeBlipElement(blip: LocalBlip) {
    const e = (blip.element = document.createElementNS(svgNS, "rect") as any);
    e.blip = blip;
    this.group.appendChild(e);
    e.setAttribute("width", "1");
    e.setAttribute("class", "blip l" + blip.level);
    this.blips.push(blip);
    this.update(blip);
    e.addEventListener("click", () => this.context.onClick(blip));
    e.addEventListener("mouseenter", () => this.context.onHover(blip));
    e.addEventListener("mouseleave", () => this.context.onHover(undefined));
  }

  updateAll() {
    const time = this.context.time;
    const scale = this.board.scale;

    let prevBlip: LocalBlip | undefined = undefined;
    for (let i = this.blips.length - 1; i >= 0; --i) {
      const blip = this.blips[i];
      const changed = this.update(blip);
      //if (!changed) return;
      if (prevBlip) {
        const prevBlip = this.blips[i + 1];
        if (prevBlip.y + prevBlip.h > blip.y) {
          blip.timeEnd = time - scale.scaleInverted(prevBlip.y);
          this.update(blip);
          this.blips.splice(i + 1, 1);
          this.group.removeChild(prevBlip.element);
        }
      }
      prevBlip = blip;
    }

    //this.markOrder();
  }

  private markOrder() {
    for (let i = 0; i < this.blips.length; ++i) {
      const blip = this.blips[i];
      blip.element.setAttribute("opacity", (i * 1.0) / this.blips.length);
    }
  }

  private update(blip: LocalBlip) {
    const time = this.context.time;
    const scale = this.board.scale;

    const se = scale.scale(time - blip.timeEnd);
    const sb = scale.scale(time - blip.timeBegin);

    const element = blip.element;

    blip.h = this.round(Math.max(scale.settings.blipSize, sb - se));
    const y = this.round(sb - blip.h);

    if (blip.y == y) return false;

    blip.y = y;

    element.setAttribute("height", blip.h);
    element.setAttribute("y", blip.y);

    return true;
  }

  round(v: number) {
    return Math.round(v * 10) / 10;
  }
}

export class Board {
  group = document.createElementNS(svgNS, "g");

  lanes: {
    [name: string]: Lane;
  } = {};

  scale: Scale;

  constructor(private context: Context) {
    this.scale = new Scale(scaleSettings);
  }

  add(blip: Blip) {
    let lane = this.lanes[blip.lane];
    if (!lane) {
      lane = this.lanes[blip.lane] = this.makeLane(blip.lane);
      this.updateLayout();
    }
    lane.add(blip);
  }

  private makeLane(laneName: string) {
    const lane = new Lane(this, this.context);
    this.group.appendChild(lane.group);
    return lane;
  }

  update() {
    for (let l in this.lanes) {
      this.lanes[l].update();
    }
    this.updateLayout();
  }

  private updateLayout() {
    const laneNames = Object.keys(this.lanes);
    laneNames.sort();
    const count = laneNames.length;
    const f = 1.0 / count;
    const weights = this.calculateLaneWeights();
    let totalWeight = 0;
    for (let laneName in this.lanes) totalWeight += weights[laneName];
    if (totalWeight === 0) totalWeight = 1;
    let x = 0;
    for (let i = 0; i < count; ++i) {
      const laneName = laneNames[i];
      const lane = this.lanes[laneName];
      const relativeWeight = weights[laneName] / totalWeight;
      const w = relativeWeight;
      lane.group.setAttribute("transform", `translate(${x}, 0) scale(${w}, 1)`);
      x += w;
    }
  }

  private calculateLaneWeights() {
    const weights: {
      [laneName: string]: number;
    } = {};
    const db = this.scale.settings.displayBegin;
    const de = this.scale.settings.displayEnd;
    for (let laneName in this.lanes) {
      const lane = this.lanes[laneName];
      let minY = de;
      for (let channelName in lane.channels) {
        const channel = lane.channels[channelName];
        const latestBlip = channel.getLatestBlip();
        if (!latestBlip) continue;
        if (latestBlip.y < minY) minY = latestBlip.y;
      }
      if (minY < db) minY = db;
      if (minY > de) minY = de;
      let w = de - minY;
      w = Math.min(w, (de - db) * 0.8);
      weights[laneName] = w;
      //weights[laneName] = 20;
    }
    return weights;
  }
}

class Scale {
  t0: number;
  t1: number;
  p0: number;
  p1: number;
  l0: number;
  l1: number;
  f: number;
  g: number;

  constructor(public readonly settings: ScaleSettings) {
    this.t0 = settings.timeBegin;
    this.t1 = settings.timeEnd;
    this.p0 = settings.displayBegin;
    this.p1 = settings.displayEnd;

    this.l0 = Math.log(this.t0);
    this.l1 = Math.log(this.t1);

    this.f = (this.p1 - this.p0) / (this.l1 - this.l0);
    this.g = this.l0 - this.p0 / this.f;
  }

  scale(time: number) {
    if (time < this.t0) return this.p0;

    return this.p0 + this.f * (Math.log(time) - this.l0);
  }

  scaleInverted(y: number) {
    return Math.exp(this.l0 + (y - this.p0) / this.f);
  }
}

const scaleSettings: ScaleSettings = {
  timeBegin: 1 * 1000,
  timeEnd: 30 * 24 * 3600 * 1000,
  displayBegin: 0,
  displayEnd: 1080,
  blipSize: 20
};
