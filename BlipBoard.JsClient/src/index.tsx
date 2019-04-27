import React from "react";
import ReactDom from "react-dom";
import { Chrome } from "./chrome";
import "./style.css";
import "@blueprintjs/core/lib/css/blueprint.css";
import { getConnection } from "./connections";

const app = document.getElementById("app");

const connection = getConnection();

const Usage = () => <div>Please use the boardId or boardUrl parameters.</div>;

if (connection) {
  ReactDom.render(<Chrome connection={connection} />, app);
} else {
  ReactDom.render(<Usage />, app);
}
