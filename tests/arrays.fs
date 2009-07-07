needs lib/arrays.fs

: words ( -- addr ) { 1 2 3 4 15 1000 } ; inline

: bytes ( -- addr ) c{ 41 42 43 } ; inline

: word-nth ( n -- v ) 2* words + flash@ ;

: byte-nth ( n -- v ) bytes + flashc@ ;

: main ( -- ) 3 word-nth 2 byte-nth 2drop ;
