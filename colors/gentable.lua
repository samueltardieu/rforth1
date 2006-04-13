function rgb_to_hsl (r, g, b)
  local v, m, vm, r2, g2, b2, h, s, l
  v = math.max (r, g, b)
  m = math.min (r, g, b)
  l = (m+v)/2
  if l <= 0 then return h, s, l end
  vm = v - m
  if (vm > 0) then s = vm / ((l < 0.5) and (v+m) or (2-v-m))
  else return h, vm, l end
  r2, g2, b2 = (v-r)/vm, (v-g)/vm, (v-b)/vm
  if r == v then h = g == m and 5 + b2 or 1 - g2
  elseif g == v then h = b == m and 1 + r2 or 3 - b2
  else h = r == m and 3 + g2 or 5 - r2 end
  return h/6, s, l
end

function norm(f)
  if f == nil then return 0 end
  return f-math.floor(f) >= 0.5 and math.ceil(f*31) or math.floor(f*31)
end

print ("module rgb_to_hsl (r, g, b, h, s, l);")
print ("  input  [4:0] r, g, b;")
print ("  output [4:0] h, s, l;")
print ("  wire   [4:0] r, g, b;")
print ("  reg    [4:0] h, s, l;")
print ()
print ("  always @(r or g or b)")
print ("    case ({r,g,b})  // synthesis_parallel_case")
for r=0,255,8 do for g=0,255,8 do for b=0,255,8 do
  h, s, l = rgb_to_hsl (r/255, g/255, b/255)
  h, s, l = norm(h), norm(s), norm(l)
  print ("      {5'd" .. r/8 .. ",5'd" .. g/8 .. ",5'd" .. b/8 .. "}: " ..
	 "begin h <= " .. h .. "; s <= " .. s .. "; l <= " .. l .. "; end")
end end end
print ("    endcase")
print ("endmodule")