prefix

code foo
  movff RCREG INDF0
  return
;code

code bar
  movff INDF0 RCREG
  return ,s
;code

: main foo bar ;
