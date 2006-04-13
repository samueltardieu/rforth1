// This module implements a SPI byte-oriented master interface with
// flow-control.
//
// (c) 2005 Samuel Tardieu <sam@rfc1149.net>
//
// Expected arguments:
//    - Inputs
//         - clock: it will be divided (see SPICLOCKDIV in the code)
//         - reset: it must be maintained at least SPICLOCKDIV full cycles
//         - outbuffer0 .. outbufferF: the data to write
//         - towrite: the number of bytes to write, starting from outbuffer0
//         - toread: the number of bytes to read, starting from inbuffer0
//         - enable: a signal indicating that towrite has changed
//           and that the SPI dialog must be initiated; this signal is
//           sampled on a positive clock edge and must be maintained
//           until the irq has been acknowledged
//         - sdi: SPI data input
//         - rts: peer is ready to send or receive data when a low pulse
//           is generated
//         - statusread: a bit toggled each time status is read
//         - picintr: a bit toggle each time status has changed
//    - Outputs
//         - inbuffer0 .. inbufferF: the data that has been read
//         - sclk: SPI clock
//         - sdo: SPI data output
//         - irq: a signal indicating that the requested operation has
//           completed; it will go down when enable goes down
//         - statusirq: signal a status change
//         - status: the current status

// The SPI clock will be clock/SPICLOCKDIV/2. For example, for an input
// clock at 60MHz and a SPI clock at 5MHz use a 6 divisor.
`define SPICLOCKDIV 6

// Status read command
`define STATUS_READ 8'hAA

module spi (clock, reset, sclk, sdi, sdo,
             inbuffer0, inbuffer1, inbuffer2, inbuffer3,
             inbuffer4, inbuffer5, inbuffer6, inbuffer7,
             inbuffer8, inbuffer9, inbufferA, inbufferB,
             inbufferC, inbufferD, inbufferE, inbufferF,
             outbuffer0, outbuffer1, outbuffer2, outbuffer3,
             outbuffer4, outbuffer5, outbuffer6, outbuffer7,
             outbuffer8, outbuffer9, outbufferA, outbufferB,
             outbufferC, outbufferD, outbufferE, outbufferF,
             toread,  towrite, enable, irq, picintr, rts,
             status, statusirq, statusread, picready, state);

    output picready;
    output [1:0] state;
    input clock;
    input reset;
    output sclk;
    input  sdi, statusread;
    output sdo, statusirq;
    input  rts, picintr;
    output [7:0] inbuffer0, inbuffer1, inbuffer2, inbuffer3,
                 inbuffer4, inbuffer5, inbuffer6, inbuffer7,
                 inbuffer8, inbuffer9, inbufferA, inbufferB,
                 inbufferC, inbufferD, inbufferE, inbufferF,
                 status;
    input [7:0]  outbuffer0, outbuffer1, outbuffer2, outbuffer3,
                 outbuffer4, outbuffer5, outbuffer6, outbuffer7,
                 outbuffer8, outbuffer9, outbufferA, outbufferB,
                 outbufferC, outbufferD, outbufferE, outbufferF,
                 toread,  towrite;
    input        enable;
    output       irq;

    wire         clock, reset, sclk, sdi, sdo, enable, picintr,
                 statusread;

    reg [7:0]    inbuffer0, inbuffer1, inbuffer2, inbuffer3,
                 inbuffer4, inbuffer5, inbuffer6, inbuffer7,
                 inbuffer8, inbuffer9, inbufferA, inbufferB,
                 inbufferC, inbufferD, inbufferE, inbufferF,
                 status;

    wire [7:0]   outbuffer0, outbuffer1, outbuffer2, outbuffer3,
                 outbuffer4, outbuffer5, outbuffer6, outbuffer7,
                 outbuffer8, outbuffer9, outbufferA, outbufferB,
                 outbufferC, outbufferD, outbufferE, outbufferF,
                 toread,  towrite;

    reg [7:0]    engine_toread, engine_towrite;
    wire [127:0] engine_inbuffer;
    reg [127:0]  engine_outbuffer;
    wire         engine_irq, engine_enable;
    wire         spiclockp, spiclockn;
    reg          irq, statusirq, oldpicintr, statuschanged;

    spi_clock_divisor #(.clkdiv(`SPICLOCKDIV)) divisor
      (.reset(reset), .clkin(clock), .clkoutp(spiclockp), .clkoutn(spiclockn));

    spi_engine engine (.clock(clock), .reset(reset),
                       .sclk(sclk), .sdi(sdi), .sdo(sdo),
                       .indata(engine_inbuffer), .outdata(engine_outbuffer),
                       .toread(engine_toread), .towrite(engine_towrite),
                       .enable(engine_enable), .irq(engine_irq),
                       .rts(rts),
                       .spiclockp(spiclockp), .spiclockn(spiclockn),
                       .picready(picready));

    reg [1:0]    state;
    reg          prevstatusread;

`define STATE_IDLE                0
`define STATE_TRANSMITTING_USER   1
`define STATE_TRANSMITTING_STATUS 2
`define STATE_SIGNALLING          3

    always @(posedge clock or negedge reset)
      if (~reset)
        begin
           state            <= `STATE_IDLE;
           status           <= 0;
           prevstatusread   <= 0;
           irq              <= 0;
           statusirq        <= 0;
           oldpicintr       <= 0;
           statuschanged    <= 0;
        end
      else
        begin
           if (picintr != oldpicintr) statuschanged <= 1;
           if (statusread != prevstatusread)
             begin
                prevstatusread <= statusread;
                statusirq <= 0;
             end
           casex ({state, enable, engine_irq, irq, statusirq, statuschanged})
             {2'd`STATE_IDLE, 1'b1, 1'b?, 1'b0, 1'b?, 1'b?}:
               // Start user request if no user IRQ is pending
               begin
                  status[7] <= 0;        // Invalidate status
                  statuschanged <= 1;
                  engine_outbuffer <=
                              {outbuffer0, outbuffer1, outbuffer2, outbuffer3,
                               outbuffer4, outbuffer5, outbuffer6, outbuffer7,
                               outbuffer8, outbuffer9, outbufferA, outbufferB,
                               outbufferC, outbufferD, outbufferE, outbufferF};
                  engine_toread  <= toread;
                  engine_towrite <= towrite;
                  state          <= `STATE_TRANSMITTING_USER;
               end
             {2'd`STATE_IDLE, 1'b?, 1'b?, 1'b?, 1'b?, 1'b1}:
               // Start status request if status has changed
               begin
                  irq              <= 0;
                  engine_outbuffer <= {`STATUS_READ, 120'd0};
                  engine_toread    <= 1;
                  engine_towrite   <= 1;
                  state            <= `STATE_TRANSMITTING_STATUS;
                  status[7]        <= 0; // Invalidate status
                  oldpicintr       <= picintr;
                  statuschanged    <= 0;
               end // case: {2'd`STATE_IDLE, 1'b?, 1'b?, 1'b?, 1'b?, 1'b1}
             /*
             {2'd`STATE_TRANSMITTING_USER, 1'b0, 1'b?, 1'b?, 1'b?, 1'b?}:
               // Request has been aborted by user
               begin
                  state <= `STATE_SIGNALLING;
                  irq   <= 0;
               end
              */
             {2'd`STATE_TRANSMITTING_USER, 1'b?, 1'b1, 1'b?, 1'b?, 1'b?}:
               // Receive answer for user
               begin
                  {inbuffer0, inbuffer1, inbuffer2, inbuffer3,
                   inbuffer4, inbuffer5, inbuffer6, inbuffer7,
                   inbuffer8, inbuffer9, inbufferA, inbufferB,
                   inbufferC, inbufferD, inbufferE, inbufferF}
                    <= engine_inbuffer;
                  irq          <= 1;
                  state        <= `STATE_SIGNALLING;
               end
             {2'd`STATE_TRANSMITTING_STATUS, 1'b?, 1'b1, 1'b?, 1'b?, 1'b?}:
               // Receive status information
               begin
                  status <= {1'b1, engine_inbuffer[126:120]};
                  if ({1'b1, engine_inbuffer[126:120]} != status)
                    statusirq <= 1;
                  state <= `STATE_SIGNALLING;
               end
             {2'd`STATE_SIGNALLING, 1'b0, 1'b?, 1'b?, 1'b?, 1'b?}:
               state <= `STATE_IDLE;
           endcase
        end

    assign engine_enable = state == `STATE_TRANSMITTING_USER |
                           state == `STATE_TRANSMITTING_STATUS;

endmodule

module spi_engine (clock, reset, sclk, sdi, sdo,
                    indata, outdata,
                    toread,  towrite, enable,
                    irq, rts, spiclockp, spiclockn, picready);

    output         picready;
    input          clock, spiclockp, spiclockn;
    input          reset;
    output         sclk;
    input          sdi;
    output         sdo;
    input          rts;
    output [127:0] indata;
    input  [127:0] outdata;
    input  [7:0]   toread,  towrite;
    input          enable;
    output         irq;

    wire           clock, reset, sclk, sdi, sdo, enable, irq, rts;

    wire [7:0]     toread,  towrite;

    wire [127:0]   indata, outdata, inbuffer;
    reg  [127:0]   shiftinbuffer;

    wire         emit_enable, emit_sclk, emit_done;
    wire         receive_enable, receive_sclk, receive_done;
    wire         spiclockp, spiclockn;

    reg [2:0]    state;

    reg [4:0]    toshift;

    wire [4:0]   inbytes;

    reg          picready;
    wire         start_emitting, start_receiving;

    reg          rts_r, rts_rr;

    wire rts_edge = ~rts_r & rts_rr;

    spi_emit_data emitter (.clock(clock), .reset(reset),
                           .enable(emit_enable), .data(outdata),
                           .count(towrite[4:0]), .sclk(emit_sclk),
                           .sdo(sdo), .done(emit_done),
                           .spiclockp(spiclockp), .spiclockn(spiclockn),
                           .picready(picready), .starting(start_emitting));

    spi_receive_data receiver (.clock(clock), .reset(reset),
                               .enable(receive_enable), .sdi(sdi),
                               .count(toread[4:0]), .sclk(receive_sclk),
                               .done(receive_done), .data(inbuffer),
                               .spiclockp(spiclockp), .spiclockn(spiclockn),
                               .picready(picready),
                               .starting(start_receiving));

`define STATE_IDLE 0
`define STATE_EMITTING 1
`define STATE_RECEIVING 2
`define STATE_SHIFTING 3
`define STATE_SIGNALLING 4

    // State changes are sampled by submodules on spiclock, which changes
    // only on negative edges of clock. This allows us to be sure that
    // at least 1/2 of clock is available to setup data for submodules.
    always @(posedge clock or negedge reset)
      if (~reset) state <= `STATE_IDLE;
      else
        // Emission and reception state machine
        casex ({state, enable, emit_done, receive_done, inbytes, toshift})
          {3'b???, 1'b0, 1'b?, 1'b?, 5'b?, 5'b?}:
            // Enable is low, reset module
            state <= `STATE_IDLE;
          {3'd`STATE_IDLE, 1'b1, 1'b?, 1'b?, 5'b?, 5'b?}:
            // Enable is high, start module
            begin
               toshift <= 5'd16 - toread[4:0];
               state <= `STATE_EMITTING;
            end
          {3'd`STATE_EMITTING, 1'b?, 1'b1, 1'b?, 5'b0, 5'b?}:
            // Data has been emitted and no data is scheduled for reception
            state <= `STATE_SIGNALLING;
          {3'd`STATE_EMITTING, 1'b?, 1'b1, 1'b?, 5'b?, 5'b?}:
            // Data has been emitted, start data reception
            state <= `STATE_RECEIVING;
          {3'd`STATE_RECEIVING, 1'b?, 1'b?, 1'b1, 5'b?, 5'b0}:
            // Data has been received and needs no shifting
            begin
               shiftinbuffer <= inbuffer;
               state <= `STATE_SIGNALLING;
            end
          {3'd`STATE_RECEIVING, 1'b1, 1'b?, 1'b1, 5'b?, 5'b?}:
            // Data has been received and needs shifting
            begin
               shiftinbuffer <= inbuffer;
               state <= `STATE_SHIFTING;
            end
          {3'd`STATE_SHIFTING, 1'b1, 1'b?, 1'b?, 5'b?, 5'b0}:
            // Shifting has ended
            state <= `STATE_SIGNALLING;
          {3'd`STATE_SHIFTING, 1'b1, 1'b?, 1'b?, 5'b?, 5'b?}:
            // We are shifting incoming data left 8 bits at a time
            begin
               shiftinbuffer <= {shiftinbuffer[119:0],8'b0};
               toshift <= toshift - 5'd1;
            end
        endcase

    always @(posedge clock or negedge reset)
      if (~reset) picready <= 0;
      else if (rts_edge) picready <= 1;
      else if (start_emitting | start_receiving) picready <= 0;

    // synchro du rts sur notre domaine de clock
    always @(posedge clock or negedge reset)
      if (~reset) begin rts_r <= 0; rts_rr <= 0; end
      else begin rts_r <= rts; rts_rr <= rts_r; end

    assign inbytes = toread[4:0];
    assign sclk = emit_sclk | receive_sclk;
    assign irq = state == `STATE_SIGNALLING;
    assign emit_enable = state == `STATE_EMITTING;
    assign receive_enable = state == `STATE_RECEIVING;
    assign indata = shiftinbuffer;

endmodule

// This module divides the clock clkin by 2*clkdiv and outputs the edges
// to be used in conditions.

module spi_clock_divisor (clkin, reset, clkoutp, clkoutn);

    parameter clkdiv = 2;

    input     clkin;
    input     reset;
    output    clkoutp, clkoutn;

    wire      clkin;
    reg       clkoutp, clkoutn;

    reg       clkbase, clkgen;
    reg [6:0] clkcnt;

    always @(posedge clkin or negedge reset)
      if (~reset)
        begin
           clkcnt  <= 0;
           clkbase <= 0;
           clkoutp <= 0;
           clkoutn <= 0;
        end
      else
        begin
           clkoutp <= 0;
           clkoutn <= 0;
           if (clkcnt == clkdiv)
             begin
                clkcnt  <= 0;
                clkbase <= ~clkbase;
                if (clkbase) clkoutn <= 1;
                else clkoutp <= 1;
             end
           else clkcnt <= clkcnt + 7'd1;
        end

    always @(negedge clkin) clkgen <= clkbase;

endmodule

module spi_emit_data (clock, reset, enable,
                       data, count, sclk, sdo, done,
                       spiclockp, spiclockn, picready, starting);

    input clock, spiclockp, spiclockn;
    input reset;
    input [127:0] data;
    input [4:0] count;
    input       enable, picready;

    output      sclk, sdo, done, starting;

    wire   clock, spiclockp, spiclockn;
    wire   enable;
    wire [127:0] data;
    wire [4:0]   count;
    reg          sclk, starting;
    wire         sdo, picready;

    reg [127:0]  latch;
    reg [7:0]    left;

    reg          ready, transmit_next;
    wire         done, transmitting;

    always @(posedge clock or negedge reset)
      if (~reset)
        begin
           ready <= 1;
           left  <= 0;
           starting <= 0;
        end
      else
        begin
           if (spiclockn)
             begin
                sclk     <= 0;
                starting <= 0;
                if (~enable)
                  begin
                     ready <= 1;
                     left  <= 0;
                  end
                else if (ready)
                  begin
                     latch <= data;
                     left  <= {count, 3'b0};
                     ready <= 0;
                  end
                else if (left > 0 && transmit_next)
                  begin
                     latch <= {latch[126:0], 1'b0};
                     left  <= left - 8'd1;
                  end
             end
           else if (spiclockp & transmitting)
             begin
                if (left[2:0] != 3'b000 | picready)
                  begin
                     sclk          <= 1;
                     transmit_next <= 1;
                     starting      <= left[2:0] == 3'b000;
                  end
                else transmit_next <= 0;
             end
        end

    assign done = ~ready & (left == 0);
    assign transmitting = ~ready & ~done;
    assign sdo = latch[127];

endmodule

module spi_receive_data (clock, reset, enable, sdi,
                          count, sclk, done, data, spiclockp, spiclockn,
                          picready, starting);

    input clock, spiclockp, spiclockn;
    input reset;
    input [4:0] count;
    input       enable, sdi, picready;
    output      sclk, starting, done;
    output [127:0] data;

    wire   clock;
    wire   enable;
    wire [127:0] data;
    wire [4:0]  count;
    reg         sclk, starting;
    wire        sdi, picready;

    reg [127:0] latch;
    reg [7:0]   left;

    reg         sample, ready, receive_next;
    wire        receiving, done;

    always @(negedge clock or negedge reset)
      if (~reset)
        begin
           ready <= 1;
           left  <= 0;
           starting <= 0;
        end
      else
        begin
           if (spiclockn)
             begin
                sclk     <= 0;
                starting <= 0;
                if (~enable)
                  begin
                     ready <= 1;
                     left  <= 0;
                  end
                else if (ready)
                  begin
                     left         <= {count, 3'b0};
                     ready        <= 0;
                  end
                else if (left > 0 && receive_next)
                  begin
                     latch <= {latch[126:0], sample};
                     left  <= left - 8'd1;
                  end
             end
           else if (spiclockp && receiving)
             if (left[2:0] != 3'b000 | picready)
               begin
                  sample       <= sdi;
                  sclk         <= 1;
                  receive_next <= 1;
                  starting     <= left[2:0] == 3'b000;
               end
             else receive_next <= 0;
        end

    assign done = ~ready & (left == 0);
    assign receiving = ~ready & ~done;
    assign data = latch;

endmodule

// This module generates an IRQ to the SH4 each time the PIC IRQ goes
// from low to high until the IRQ gets acked. Unused at this time.

module spi_irq (clock, reset, signal, irq, ack);

    input signal, ack, clock, reset;
    output irq;

    wire   clock, reset, signal, ack;
    reg    irq, prevsignal;

    always @(posedge clock or negedge reset)
      if (~reset)
        begin
           prevsignal <= 0;
           irq <= 0;
        end
      else
        begin
           if (signal && ~prevsignal) irq <= 1;
           prevsignal <= signal;
           if (ack) irq <= 0;
        end

endmodule

