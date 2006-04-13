forward emit
forward key?
forward key

0x20 constant bl

: space ( -- ) bl emit ;
: cr ( -- ) 0xa emit 0xd emit ;


code nibble-to-hex
    POSTDEC0 ,w ,a movf   \ Point onto LSB
    0xf6 movlw   \ Add 246 so that C is set for A-F and not for 0-9
    INDF0 ,f ,a addwf
    0x3a movlw   \ This doesn't affect the C bit
    C ,a btfsc   \ If C is set, we have a letter, add 7 more
    7 addlw
    POSTDEC0 ,w ,a addwf   \ Add and finishing popping
    return
;code outw


: emit-4 ( n -- ) nibble-to-hex emit ;
: emit-8 ( n -- ) dup swapf-lsb 0xf and emit-4 0xf and emit-4 ;
: . ( n -- ) 1>2 emit-8 emit-8 ;

: type ( a n -- )
  dup 0= if 2drop exit then
  cfor dup c@ emit 1+ cnext
  drop
;

python

class SQuote (Primitive):

  def run (self):
    str = compiler.parse ('"')
    parsed = str.replace ('\\n', '\r\n')
    parsed = parsed.replace ('\\t', '\t')
    data = FlashData ([ord (c) for c in parsed], str)
    compiler.push (Add (data, Number (0x8000, 16)))
    compiler.push (Number (len (parsed)))

class DotQuote (SQuote):

  def run (self):
    SQuote.run (self)
    compiler.add_call (compiler['type'])

compiler.add_primitive ('s"', SQuote)
compiler.add_primitive ('."', DotQuote)

;python

: .s ( -- )
  [char] < emit depth dup . [char] > emit
  dup if
    dup 8 > if
      drop ."  ..." 8
    then
    cfor space cr@ 1- pick . cnext
  else
    drop
  then
;

: read4 ( -- n ) key dup 65 >= if 223 and 55 - else 48 - then ;
: read8 ( -- n ) read4 swapf-lsb read4 or ;
: read16 ( -- n ) read8 read8 swap 2>1  ;
