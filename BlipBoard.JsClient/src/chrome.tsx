import React from "react";
import { Context, Blip, BlipBoardTarget, BlipBoardConnection } from "./common";
import { Board } from "./board";
import * as signalR from "@aspnet/signalr";
import { Drawer, Classes } from "@blueprintjs/core";

interface ChromeProps {
  connection: BlipBoardConnection;
}

interface ChromeState {
  hoveredBlip?: Blip;
  displayedBlip?: Blip;
}

export class Chrome extends React.Component<ChromeProps, ChromeState>
  implements BlipBoardTarget {
  boardSvg = React.createRef<SVGSVGElement>();

  boardContext: Context = {
    time: Date.now(),
    onClick: b => this.handleBlipClick(b),
    onHover: b => this.handleBlipHover(b)
  };

  board = new Board(this.boardContext);

  constructor(props: ChromeProps) {
    super(props);
    this.state = {};
  }

  componentDidMount() {
    this.boardSvg.current!.appendChild(this.board.group);

    this.props.connection.startConnection(this);
    this.handleAnimationFrame();
  }

  render() {
    const hoveredBlip = this.state.hoveredBlip;
    return (
      <div className="chrome">
        <svg
          ref={this.boardSvg}
          preserveAspectRatio="none"
          viewBox="0 0 1 1080"
          filter="url(#globalFilter)"
        >
          <filter id="globalFilter">
            <feTurbulence
              type="turbulence"
              baseFrequency="0.05"
              numOctaves="2"
              result="turbulence"
            />
            <feDisplacementMap
              in2="turbulence"
              in="SourceGraphic"
              scale="2"
              xChannelSelector="R"
              yChannelSelector="G"
            />
          </filter>
        </svg>
        {hoveredBlip && <div className="status-bar">{hoveredBlip.lane}</div>}
        <Drawer
          isOpen={!!this.state.displayedBlip}
          onClose={() => this.setState({ displayedBlip: undefined })}
        >
          <div className={Classes.DRAWER_BODY}>
            <div className={Classes.DIALOG_BODY}>
              <div className="details">
                {this.state.displayedBlip &&
                  (this.state.displayedBlip!.body ||
                    this.state.displayedBlip!.details)}
              </div>
            </div>
          </div>
        </Drawer>
      </div>
    );
  }

  addBlip(blip: any) {
    console.info("got blip");
    try {
      this.board.add(blip as Blip);
    } catch (ex) {
      console.error(ex);
    }
  }

  handleBlipClick(blip: Blip) {
    this.setState({ displayedBlip: blip });
  }

  handleBlipHover(blip?: Blip) {
    this.setState({ hoveredBlip: blip });
  }

  handleAnimationFrame() {
    this.boardContext.time = Date.now();
    this.board.update();
    requestAnimationFrame(() => this.handleAnimationFrame());
  }
}
